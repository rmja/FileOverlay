using Microsoft.Extensions.FileProviders;

namespace FileOverlay.Tests;

public class OverlayFileProviderTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PhysicalFileProvider _physicalProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public OverlayFileProviderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileOverlayTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _physicalProvider = new PhysicalFileProvider(_testDirectory);
    }

    public void Dispose()
    {
        _physicalProvider.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetFileInfo_WithoutOverlay_ShouldReturnInnerProviderFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var fileInfo = provider.GetFileInfo("test.txt");

        // Assert
        Assert.True(fileInfo.Exists);
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Equal("Original content", content);
    }

    [Fact]
    public void CreateOverlay_ShouldCreatePhysicalCopy()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var overlayFile = provider.CreateOverlay("test.txt");

        // Assert
        Assert.NotNull(overlayFile);
        Assert.True(File.Exists(overlayFile.OverlayFilePath));
        Assert.Equal("Original content", File.ReadAllText(overlayFile.OverlayFilePath));
    }

    [Fact]
    public void CreateOverlay_ShouldPreserveLastModifiedTime()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        var originalTime = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(testFile, originalTime);
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var overlayFile = provider.CreateOverlay("test.txt");

        // Assert
        var overlayTime = File.GetLastWriteTimeUtc(overlayFile.OverlayFilePath);
        Assert.Equal(originalTime, overlayTime);
    }

    [Fact]
    public void CreateOverlay_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            provider.CreateOverlay("nonexistent.txt")
        );
        Assert.Contains("nonexistent.txt", exception.Message);
    }

    [Fact]
    public void CreateOverlay_WithNestedPath_ShouldCreateDirectoryStructure()
    {
        // Arrange
        var nestedDir = Path.Combine(_testDirectory, "nested", "dir");
        Directory.CreateDirectory(nestedDir);
        var testFile = Path.Combine(nestedDir, "test.txt");
        File.WriteAllText(testFile, "Nested content");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var overlayFile = provider.CreateOverlay("nested/dir/test.txt");

        // Assert
        Assert.NotNull(overlayFile);
        Assert.True(File.Exists(overlayFile.OverlayFilePath));
        Assert.Equal("Nested content", File.ReadAllText(overlayFile.OverlayFilePath));
    }

    [Fact]
    public void GetFileInfo_AfterOverlay_ShouldReturnOverlayedFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        var provider = new OverlayFileProvider(_physicalProvider);
        var overlayFile = provider.CreateOverlay("test.txt");
        File.WriteAllText(overlayFile.OverlayFilePath, "Modified content");

        // Act
        var fileInfo = provider.GetFileInfo("test.txt");

        // Assert
        Assert.True(fileInfo.Exists);
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Equal("Modified content", content);
    }

    [Fact]
    public void GetFileInfo_WithLeadingSlash_ShouldReturnOverlayedFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        var provider = new OverlayFileProvider(_physicalProvider);
        var overlayFile = provider.CreateOverlay("/test.txt");
        File.WriteAllText(overlayFile.OverlayFilePath, "Modified content");

        // Act
        var fileInfo = provider.GetFileInfo("/test.txt");

        // Assert
        Assert.True(fileInfo.Exists);
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Equal("Modified content", content);
    }

    [Fact]
    public void GetDirectoryContents_ShouldReturnInnerProviderContents()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.txt"), "Content 2");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var contents = provider.GetDirectoryContents("");

        // Assert
        Assert.NotNull(contents);
        Assert.Equal(2, contents.Count());
    }

    [Fact]
    public void Watch_ShouldReturnInnerProviderChangeToken()
    {
        // Arrange
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var token = provider.Watch("*.txt");

        // Assert
        Assert.NotNull(token);
    }

    [Fact]
    public void CreateOverlay_InSubdirectory_WithBackslash_ShouldWork()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdirectory");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.txt");
        File.WriteAllText(testFile, "Subdirectory content");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var overlayFile = provider.CreateOverlay(@"subdirectory\test.txt");

        // Assert
        Assert.NotNull(overlayFile);
        Assert.True(File.Exists(overlayFile.OverlayFilePath));
        Assert.Equal("Subdirectory content", File.ReadAllText(overlayFile.OverlayFilePath));
    }

    [Fact]
    public void CreateOverlay_MultipleFilesInDifferentSubdirectories_ShouldWork()
    {
        // Arrange
        var subDir1 = Path.Combine(_testDirectory, "dir1");
        var subDir2 = Path.Combine(_testDirectory, "dir2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);
        File.WriteAllText(Path.Combine(subDir1, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Combine(subDir2, "file2.txt"), "Content 2");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var overlay1 = provider.CreateOverlay("dir1/file1.txt");
        var overlay2 = provider.CreateOverlay("dir2/file2.txt");
        File.WriteAllText(overlay1.OverlayFilePath, "Modified 1");
        File.WriteAllText(overlay2.OverlayFilePath, "Modified 2");

        // Assert
        var fileInfo1 = provider.GetFileInfo("dir1/file1.txt");
        var fileInfo2 = provider.GetFileInfo("dir2/file2.txt");

        using (var stream = fileInfo1.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal("Modified 1", reader.ReadToEnd());
        }

        using (var stream = fileInfo2.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal("Modified 2", reader.ReadToEnd());
        }
    }

    [Fact]
    public void CreateOverlay_InDeepNestedPath_ShouldCreateAllDirectories()
    {
        // Arrange
        var deepPath = Path.Combine(_testDirectory, "a", "b", "c", "d");
        Directory.CreateDirectory(deepPath);
        var testFile = Path.Combine(deepPath, "deep.txt");
        File.WriteAllText(testFile, "Deep content");
        var provider = new OverlayFileProvider(_physicalProvider);

        // Act
        var overlayFile = provider.CreateOverlay("a/b/c/d/deep.txt");
        File.WriteAllText(overlayFile.OverlayFilePath, "Modified deep");

        // Assert
        var fileInfo = provider.GetFileInfo("a/b/c/d/deep.txt");
        Assert.True(fileInfo.Exists);
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        Assert.Equal("Modified deep", reader.ReadToEnd());
    }

    [Fact]
    public void GetFileInfo_SubdirectoryWithLeadingSlash_ShouldReturnOverlayedFile()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.txt");
        File.WriteAllText(testFile, "Original");
        var provider = new OverlayFileProvider(_physicalProvider);
        var overlayFile = provider.CreateOverlay("/subdir/test.txt");
        File.WriteAllText(overlayFile.OverlayFilePath, "Modified");

        // Act
        var fileInfo1 = provider.GetFileInfo("/subdir/test.txt");
        var fileInfo2 = provider.GetFileInfo("subdir/test.txt");

        // Assert
        using (var stream = fileInfo1.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal("Modified", reader.ReadToEnd());
        }

        using (var stream = fileInfo2.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal("Modified", reader.ReadToEnd());
        }
    }

    [Fact]
    public async Task CreateOverlay_WithAutoRefresh_ShouldUpdateWhenSourceChanges()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        using var provider = new OverlayFileProvider(_physicalProvider);
        var overlayFile = provider.CreateOverlay("test.txt", autoRefresh: true);

        // Act - Modify the source file
        await Task.Delay(100, TestCancellationToken); // Small delay to ensure file watcher is set up
        File.WriteAllText(testFile, "Updated content");
        await Task.Delay(500, TestCancellationToken); // Wait for file change notification

        // Assert
        var content = File.ReadAllText(overlayFile.OverlayFilePath);
        Assert.Equal("Updated content", content);
    }

    [Fact]
    public async Task CreateOverlay_WithAutoRefresh_ShouldPreserveLastModifiedTime()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        using var provider = new OverlayFileProvider(_physicalProvider);
        var overlayFile = provider.CreateOverlay("test.txt", autoRefresh: true);

        // Act - Modify the source file with a specific timestamp
        await Task.Delay(100, TestCancellationToken);
        var newTime = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        File.WriteAllText(testFile, "Updated content");
        File.SetLastWriteTimeUtc(testFile, newTime);
        await Task.Delay(500, TestCancellationToken);

        // Assert
        var overlayTime = File.GetLastWriteTimeUtc(overlayFile.OverlayFilePath);
        Assert.Equal(newTime, overlayTime);
    }

    [Fact]
    public async Task CreateOverlay_WithoutAutoRefresh_ShouldNotUpdateWhenSourceChanges()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        using var provider = new OverlayFileProvider(_physicalProvider);
        var overlayFile = provider.CreateOverlay("test.txt", autoRefresh: false);

        // Act - Modify the source file
        await Task.Delay(100, TestCancellationToken);
        File.WriteAllText(testFile, "Updated content");
        await Task.Delay(500, TestCancellationToken);

        // Assert - Overlay should still have original content
        var content = File.ReadAllText(overlayFile.OverlayFilePath);
        Assert.Equal("Original content", content);
    }

    [Fact]
    public async Task Dispose_ShouldStopAutoRefresh()
    {
        // Arrange
        var explicitOverlayDirectory = Path.Combine(
            Path.GetTempPath(),
            $"FileOverlayTests_{Guid.NewGuid()}"
        );
        Directory.CreateDirectory(explicitOverlayDirectory);

        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "Original content");
        var provider = new OverlayFileProvider(
            _physicalProvider,
            new PhysicalFileProvider(explicitOverlayDirectory)
        );
        var overlayFile = provider.CreateOverlay("test.txt", autoRefresh: true);

        // Act - Dispose and then modify source
        await Task.Delay(100, TestCancellationToken);
        provider.Dispose();
        File.WriteAllText(testFile, "Updated content");
        await Task.Delay(500, TestCancellationToken);

        // Assert - Overlay should still have original content
        var content = File.ReadAllText(overlayFile.OverlayFilePath);
        Assert.Equal("Original content", content);
    }
}
