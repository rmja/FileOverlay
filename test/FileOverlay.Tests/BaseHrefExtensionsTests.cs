using Microsoft.Extensions.FileProviders;

namespace FileOverlay.Tests;

public class BaseHrefExtensionsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PhysicalFileProvider _physicalProvider;

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
}
