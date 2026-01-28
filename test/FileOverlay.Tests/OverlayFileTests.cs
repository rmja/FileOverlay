using System.Text;

namespace FileOverlay.Tests;

public class OverlayFileTests : IDisposable
{
    private readonly string _testDirectory;

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
        var overlayFile = new OverlayFile(testFile, "test.txt");

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
        var overlayFile = new OverlayFile(testFile, "test.html");

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
        var overlayFile = new OverlayFile(testFile, "test.txt");

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
        var overlayFile = new OverlayFile(testFile, "test.txt");

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
        var overlayFile = new OverlayFile(testFile, "original.txt");

        // Act & Assert
        Assert.Equal(testFile, overlayFile.OverlayFilePath);
    }

    [Fact]
    public void OriginalFilePath_ShouldReturnCorrectPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var overlayFile = new OverlayFile(testFile, "original.txt");

        // Act & Assert
        Assert.Equal("original.txt", overlayFile.OriginalFilePath);
    }
}
