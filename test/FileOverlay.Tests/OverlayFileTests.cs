using System.Text;
using Microsoft.Extensions.FileProviders;

namespace FileOverlay.Tests;

public class OverlayFileTests : IDisposable
{
    private readonly string _testDirectory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public OverlayFileTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OverlayFileTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TransformContent_ShouldModifyFileContent()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "original content", Encoding.UTF8);
        var overlayFile = new OverlayFile("test.txt", testFile);

        // Act
        overlayFile.TransformContent(content => content.ToUpperInvariant());

        // Assert
        var result = File.ReadAllText(testFile, Encoding.UTF8);
        Assert.Equal("ORIGINAL CONTENT", result);
    }

    [Fact]
    public void TransformContent_WithComplexTransformation_ShouldApplyCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.html");
        File.WriteAllText(testFile, "<html><body>Hello World</body></html>", Encoding.UTF8);
        var overlayFile = new OverlayFile("test.html", testFile);

        // Act
        overlayFile.TransformContent(content =>
            content.Replace("Hello World", "Hello FileOverlay")
        );

        // Assert
        var result = File.ReadAllText(testFile, Encoding.UTF8);
        Assert.Equal("<html><body>Hello FileOverlay</body></html>", result);
    }

    [Fact]
    public void TransformContent_WithMultilineContent_ShouldPreserveFormatting()
    {
        // Arrange
        var originalContent = "Line 1\nLine 2\nLine 3";
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, originalContent, Encoding.UTF8);
        var overlayFile = new OverlayFile("test.txt", testFile);

        // Act
        overlayFile.TransformContent(content => content.Replace("Line 2", "Modified Line 2"));

        // Assert
        var result = File.ReadAllText(testFile, Encoding.UTF8);
        Assert.Equal("Line 1\nModified Line 2\nLine 3", result);
    }

    [Fact]
    public void TransformContent_WithUtf8Characters_ShouldPreserveEncoding()
    {
        // Arrange
        var originalContent = "Hello 世界 🌍";
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, originalContent, Encoding.UTF8);
        var overlayFile = new OverlayFile("test.txt", testFile);

        // Act
        overlayFile.TransformContent(content => content + " ✓");

        // Assert
        var result = File.ReadAllText(testFile, Encoding.UTF8);
        Assert.Equal("Hello 世界 🌍 ✓", result);
    }

    [Fact]
    public void OverlayFilePath_ShouldReturnCorrectPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var overlayFile = new OverlayFile("original.txt", testFile);

        // Act & Assert
        Assert.Equal(testFile, overlayFile.OverlayFilePath);
    }

    [Fact]
    public void OriginalFilePath_ShouldReturnCorrectPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var overlayFile = new OverlayFile("original.txt", testFile);

        // Act & Assert
        Assert.Equal("original.txt", overlayFile.RelativeFilePath);
    }

    [Fact]
    public async Task TransformContent_WithAutoRefresh_ShouldReapplyTransformWhenSourceChanges()
    {
        // Arrange
        var sourceDir = Path.Combine(Path.GetTempPath(), $"SourceDir_{Guid.NewGuid()}");
        Directory.CreateDirectory(sourceDir);
        var testFile = Path.Combine(sourceDir, "test.txt");
        File.WriteAllText(testFile, "original content");

        using var sourceProvider = new PhysicalFileProvider(sourceDir);
        using var overlayProvider = new OverlayFileProvider(sourceProvider);
        var overlayFile = overlayProvider.CreateOverlay("test.txt", autoRefresh: true);

        // Act - Apply transform with auto-refresh
        overlayFile.TransformContent(content => content.ToUpperInvariant());

        await Task.Delay(100, TestCancellationToken);
        File.WriteAllText(testFile, "new content");
        await Task.Delay(500, TestCancellationToken);

        // Assert
        var result = File.ReadAllText(overlayFile.OverlayFilePath);
        Assert.Equal("NEW CONTENT", result);

        // Cleanup
        Directory.Delete(sourceDir, true);
    }

    [Fact]
    public async Task TransformContent_WithAutoRefresh_ShouldReapplyMultipleTransformsInOrder()
    {
        // Arrange
        var sourceDir = Path.Combine(Path.GetTempPath(), $"SourceDir_{Guid.NewGuid()}");
        Directory.CreateDirectory(sourceDir);
        var testFile = Path.Combine(sourceDir, "test.txt");
        File.WriteAllText(testFile, "hello world");

        using var sourceProvider = new PhysicalFileProvider(sourceDir);
        using var overlayProvider = new OverlayFileProvider(sourceProvider);
        var overlayFile = overlayProvider.CreateOverlay("test.txt", autoRefresh: true);

        // Act - Apply multiple transforms
        overlayFile.TransformContent(content => content.Replace("hello", "goodbye"));
        overlayFile.TransformContent(content => content.Replace("world", "universe"));
        overlayFile.TransformContent(content => content.ToUpperInvariant());

        await Task.Delay(100, TestCancellationToken);
        File.WriteAllText(testFile, "hello world again");
        await Task.Delay(500, TestCancellationToken);

        // Assert - All transforms should be reapplied in order
        var result = File.ReadAllText(overlayFile.OverlayFilePath);
        Assert.Equal("GOODBYE UNIVERSE AGAIN", result);

        // Cleanup
        Directory.Delete(sourceDir, true);
    }

    [Fact]
    public async Task TransformContent_WithAutoRefresh_AndDispose_ShouldStopWatching()
    {
        // Arrange
        var sourceDir = Path.Combine(Path.GetTempPath(), $"SourceDir_{Guid.NewGuid()}");
        Directory.CreateDirectory(sourceDir);
        var testFile = Path.Combine(sourceDir, "test.txt");
        File.WriteAllText(testFile, "original");

        var explicitOverlayDirectory = Path.Combine(
            Path.GetTempPath(),
            $"FileOverlayTests_{Guid.NewGuid()}"
        );
        Directory.CreateDirectory(explicitOverlayDirectory);

        using var sourceProvider = new PhysicalFileProvider(sourceDir);
        var overlayProvider = new OverlayFileProvider(
            sourceProvider,
            new PhysicalFileProvider(explicitOverlayDirectory)
        );
        var overlayFile = overlayProvider.CreateOverlay("test.txt", autoRefresh: true);

        // Act - Apply transform with auto-refresh, then dispose
        overlayFile.TransformContent(content => content.ToUpperInvariant());
        await Task.Delay(100, TestCancellationToken);

        overlayProvider.Dispose();

        File.WriteAllText(testFile, "modified");
        await Task.Delay(500, TestCancellationToken);

        // Assert - Transform should NOT be reapplied after dispose
        var result = File.ReadAllText(overlayFile.OverlayFilePath);
        Assert.Equal("ORIGINAL", result); // Should still have the original transformed content

        // Cleanup
        Directory.Delete(sourceDir, true);
    }
}
