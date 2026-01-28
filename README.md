# FileOverlay

A file provider wrapper for ASP.NET Core that enables runtime modification of static files. Useful for rewriting HTML base href attributes when hosting under a path base, while preserving proper HTTP caching headers.


When hosting ASP.NET Core applications (especially SPAs) under a path base like `/myapp`, you often need to dynamically rewrite the `<base href="/">` tag in your HTML to match the deployment path. Traditional solutions either:

- Require build-time configuration (different builds for different deployments)
- Use middleware that breaks HTTP caching (loses ETags, LastModified headers)
- Manually copy and modify files on application startup

FileOverlay solves this by creating a transparent file provider overlay that:

- ✅ Modifies files at runtime
- ✅ Preserves all HTTP caching headers (ETags, LastModified)
- ✅ Works seamlessly with ASP.NET Core's static file middleware
- ✅ Supports any IFileProvider source

## Installation

```bash
dotnet add package FileOverlay
```

## Usage Example: Replacing `<base href="/">` with PathBase

Here's a complete example showing how to rewrite the base href in your HTML files to use the application's PathBase:

```csharp
using FileOverlay;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Get the PathBase from configuration or environment
var pathBase = app.Configuration["PathBase"] ?? "/";

// Create an overlay that rewrites base href in index.html
var fileProvider = app.Environment.WebRootFileProvider.WithBaseHrefRewrite(
    pathBase: pathBase,
    "index.html" // Can specify multiple files: "index.html", "about.html", etc.
);

// Use the overlay file provider for static files
app.UseStaticFiles(new StaticFileOptions 
{ 
    FileProvider = fileProvider 
});

app.Run();
```

### What it does

If your `index.html` contains:

```html
<!DOCTYPE html>
<html>
<head>
    <base href="/" />
    <title>My App</title>
</head>
<body>
    <!-- Your app content -->
</body>
</html>
```

And you set `PathBase` to `/myapp`, the served HTML will automatically become:

```html
<!DOCTYPE html>
<html>
<head>
    <base href="/myapp/" />
    <title>My App</title>
</head>
<body>
    <!-- Your app content -->
</body>
</html>
```

## Advanced Usage: Custom Transformations

For more complex scenarios, you can use the low-level API to create custom file transformations:

```csharp
var overlay = new OverlayFileProvider(app.Environment.WebRootFileProvider);

// Create an overlay for a specific file
var indexFile = overlay.CreateOverlay("index.html");

// Apply custom transformations
indexFile.TransformContent(content =>
{
    // Replace any placeholder with runtime values
    content = content.Replace("{{API_URL}}", app.Configuration["ApiUrl"]);
    content = content.Replace("{{VERSION}}", app.Configuration["Version"]);
    return content;
});

// Use the overlay for serving static files
app.UseStaticFiles(new StaticFileOptions { FileProvider = overlay });
```

## How It Works

1. **Overlay Creation**: When you call `CreateOverlay()`, FileOverlay creates a physical copy of the file in a temporary directory
2. **Transformation**: You can then transform the content using `TransformContent()`
3. **Transparent Serving**: The overlay provider intercepts requests for overlayed files and serves the modified versions
4. **Cache Preservation**: The overlay preserves the original file's `LastModified` timestamp, ensuring proper HTTP caching with ETags

Files that are not overlayed are served directly from the original provider without any overhead.

## Benefits

- **Runtime Configuration**: No need for different builds per environment
- **Proper HTTP Caching**: Maintains ETags and LastModified headers for optimal performance
- **SPA Friendly**: Perfect for SPAs deployed under path bases
- **Flexible**: Works with any `IFileProvider` implementation
- **Minimal Overhead**: Only specified files are copied and modified

## Common Scenarios

### Hosting Multiple SPAs Under Different Paths

```csharp
// App 1 at /app1
app.Map("/app1", app1 =>
{
    var fileProvider = env.WebRootFileProvider.WithBaseHrefRewrite("/app1", "index.html");
    app1.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
});

// App 2 at /app2
app.Map("/app2", app2 =>
{
    var fileProvider = env.WebRootFileProvider.WithBaseHrefRewrite("/app2", "index.html");
    app2.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
});
```

## License

MIT

## Contributing

Contributions are welcome! Please open an issue or submit a pull request on [GitHub](https://github.com/rmja/FileOverlay).
