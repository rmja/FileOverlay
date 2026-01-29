using Microsoft.Extensions.FileProviders;

namespace FileOverlay.Tests;

public class BaseHrefExtensionsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PhysicalFileProvider _physicalProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public BaseHrefExtensionsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"BaseHrefTests_{Guid.NewGuid()}");
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
    public void WithBaseHrefRewrite_ShouldRewriteSelfClosingBaseTag()
    {
        // Arrange
        var html =
            @"<!DOCTYPE html>
<html>
<head>
    <base href=""/"" />
    <title>Test</title>
</head>
<body>Content</body>
</html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains(@"<base href=""/myapp/"" />", content);
    }

    [Fact]
    public void WithBaseHrefRewrite_ShouldRewriteNonSelfClosingBaseTag()
    {
        // Arrange
        var html = @"<html><head><base href=""/""></head><body>Content</body></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains(@"<base href=""/myapp/"" />", content);
    }

    [Fact]
    public void WithBaseHrefRewrite_WithTrailingSlash_ShouldNotAddExtraSlash()
    {
        // Arrange
        var html = @"<html><head><base href=""/"" /></head></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp/", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains(@"<base href=""/myapp/"" />", content);
        Assert.DoesNotContain(@"<base href=""/myapp//"" />", content);
    }

    [Fact]
    public void WithBaseHrefRewrite_WithoutTrailingSlash_ShouldAddSlash()
    {
        // Arrange
        var html = @"<html><head><base href=""/"" /></head></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains(@"<base href=""/myapp/"" />", content);
    }

    [Fact]
    public void WithBaseHrefRewrite_WithEmptyPathBase_ShouldNotModifyContent()
    {
        // Arrange
        var html = @"<html><head><base href=""/"" /></head></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Equal(html, content);
    }

    [Fact]
    public void WithBaseHrefRewrite_WithNullPathBase_ShouldNotModifyContent()
    {
        // Arrange
        var html = @"<html><head><base href=""/"" /></head></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite(null!, "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Equal(html, content);
    }

    [Fact]
    public void WithBaseHrefRewrite_WithMultipleFiles_ShouldRewriteAll()
    {
        // Arrange
        var html = @"<html><head><base href=""/"" /></head></html>";
        File.WriteAllText(Path.Combine(_testDirectory, "index.html"), html);
        File.WriteAllText(Path.Combine(_testDirectory, "404.html"), html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp", "index.html", "404.html");

        // Assert
        var indexFileInfo = provider.GetFileInfo("index.html");
        using (var stream = indexFileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            Assert.Contains(@"<base href=""/myapp/"" />", content);
        }

        var notFoundFileInfo = provider.GetFileInfo("404.html");
        using (var stream = notFoundFileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            Assert.Contains(@"<base href=""/myapp/"" />", content);
        }
    }

    [Fact]
    public void WithBaseHrefRewrite_WithCaseInsensitiveTag_ShouldRewrite()
    {
        // Arrange
        var html = @"<html><head><BASE HREF=""/"" /></head></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains(@"<base href=""/myapp/"" />", content);
    }

    [Fact]
    public void WithBaseHrefRewrite_WithExtraSpaces_ShouldRewrite()
    {
        // Arrange
        var html = @"<html><head><base   href  =  ""/"" /></head></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        var provider = _physicalProvider.WithBaseHrefRewrite("/myapp", "index.html");
        var fileInfo = provider.GetFileInfo("index.html");

        // Assert
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains(@"<base href=""/myapp/"" />", content);
    }

    [Fact]
    public async Task WithBaseHrefRewrite_WithAutoRefresh_ShouldReapplyTransformWhenSourceChanges()
    {
        // Arrange
        var html = @"<html><head><base href=""/"" /></head><body>Content</body></html>";
        var testFile = Path.Combine(_testDirectory, "index.html");
        File.WriteAllText(testFile, html);

        // Act
        using var provider = _physicalProvider.WithBaseHrefRewrite(
            "/myapp",
            autoRefresh: true,
            "index.html"
        );

        // Verify initial transform
        var fileInfo = provider.GetFileInfo("index.html");
        using (var stream = fileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            Assert.Contains(@"<base href=""/myapp/"" />", content);
        }

        // Modify source file
        await Task.Delay(100, TestCancellationToken);
        var updatedHtml =
            @"<html><head><base href=""/"" /></head><body>Updated Content</body></html>";
        File.WriteAllText(testFile, updatedHtml);
        await Task.Delay(500, TestCancellationToken);

        // Assert - Transform should be reapplied
        fileInfo = provider.GetFileInfo("index.html");
        using (var stream = fileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            Assert.Contains(@"<base href=""/myapp/"" />", content);
            Assert.Contains("Updated Content", content);
        }
    }

    [Fact]
    public async Task WithBaseHrefRewrite_WithAutoRefresh_MultipleFiles_ShouldUpdateAllFiles()
    {
        // Arrange
        var html1 = @"<html><head><base href=""/"" /></head><body>File 1</body></html>";
        var html2 = @"<html><head><base href=""/"" /></head><body>File 2</body></html>";
        var file1 = Path.Combine(_testDirectory, "page1.html");
        var file2 = Path.Combine(_testDirectory, "page2.html");
        File.WriteAllText(file1, html1);
        File.WriteAllText(file2, html2);

        // Act
        using var provider = _physicalProvider.WithBaseHrefRewrite(
            "/myapp",
            autoRefresh: true,
            "page1.html",
            "page2.html"
        );

        await Task.Delay(100, TestCancellationToken);

        // Update both source files
        File.WriteAllText(
            file1,
            @"<html><head><base href=""/"" /></head><body>Updated File 1</body></html>"
        );
        File.WriteAllText(
            file2,
            @"<html><head><base href=""/"" /></head><body>Updated File 2</body></html>"
        );
        await Task.Delay(500, TestCancellationToken);

        // Assert - Both files should have transforms reapplied
        var fileInfo1 = provider.GetFileInfo("page1.html");
        using (var stream = fileInfo1.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            Assert.Contains(@"<base href=""/myapp/"" />", content);
            Assert.Contains("Updated File 1", content);
        }

        var fileInfo2 = provider.GetFileInfo("page2.html");
        using (var stream = fileInfo2.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            Assert.Contains(@"<base href=""/myapp/"" />", content);
            Assert.Contains("Updated File 2", content);
        }
    }
}
