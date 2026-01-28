using System.Reflection;
using FileOverlay;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace FileOverlay;

/// <summary>
/// A file provider that wraps an existing file provider and allows creating physical copies
/// of specific files in a temporary overlay directory for modification.
/// </summary>
/// <remarks>
/// This provider is useful when you need to serve modified versions of static files while
/// preserving proper file metadata (LastModified, Content-Length, ETags) for HTTP caching.
/// Files not explicitly overlayed are served directly from the inner provider.
/// </remarks>
public class OverlayFileProvider : IFileProvider
{
    private readonly IFileProvider _innerProvider;
    private readonly string _overlayDirectory;
    private readonly PhysicalFileProvider _overlayProvider;
    private readonly HashSet<string> _overlayedFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayFileProvider"/> class.
    /// </summary>
    /// <param name="innerProvider">The underlying file provider to wrap.</param>
    public OverlayFileProvider(IFileProvider innerProvider)
    {
        _innerProvider = innerProvider;

        // See https://github.com/dotnet/runtime/blob/9e84f5eba0effdf4f75f52a5c1465e08918a75a4/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs#L235
        var applicationName = Assembly.GetEntryAssembly()?.GetName().Name;
        var directoryPrefix = applicationName is not null ? applicationName + "-" : null;
        _overlayDirectory = Directory.CreateTempSubdirectory(directoryPrefix).FullName;
        _overlayProvider = new PhysicalFileProvider(_overlayDirectory);
    }

    /// <summary>
    /// Creates a physical copy of a file from the inner provider in the overlay directory.
    /// </summary>
    /// <param name="filePath">The path of the file to overlay, relative to the provider root.</param>
    /// <returns>An <see cref="OverlayFile"/> instance that can be used to transform the overlayed file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist in the inner provider.</exception>
    /// <remarks>
    /// The overlayed file preserves the LastModified timestamp from the original file.
    /// Once a file is overlayed, calls to <see cref="GetFileInfo"/> will return the overlay version.
    /// </remarks>
    public OverlayFile CreateOverlay(string filePath)
    {
        // Normalize the path to remove leading slashes
        var sourceFileInfo = _innerProvider.GetFileInfo(filePath);
        if (!sourceFileInfo.Exists)
        {
            throw new FileNotFoundException($"Source file not found: {filePath}");
        }

        var normalizedPath = filePath.TrimStart('/');
        var destinationPath = Path.Combine(_overlayDirectory, normalizedPath);
        var destinationDir = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDir);

        using (var sourceStream = sourceFileInfo.CreateReadStream())
        using (var destStream = File.Create(destinationPath))
        {
            sourceStream.CopyTo(destStream);
        }

        // Preserve original LastModified time
        File.SetLastWriteTimeUtc(destinationPath, sourceFileInfo.LastModified.UtcDateTime);

        _overlayedFiles.Add(normalizedPath);

        return new OverlayFile(destinationPath, filePath);
    }

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath) =>
        _innerProvider.GetDirectoryContents(subpath);

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the overlayed file info if the file has been overlayed via <see cref="CreateOverlay"/>,
    /// otherwise returns the file info from the inner provider.
    /// </remarks>
    public IFileInfo GetFileInfo(string subpath)
    {
        var normalizedPath = subpath.TrimStart('/');

        if (_overlayedFiles.Contains(normalizedPath))
        {
            return _overlayProvider.GetFileInfo(subpath);
        }

        return _innerProvider.GetFileInfo(subpath);
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter) => _innerProvider.Watch(filter);
}
