using System.Reflection;
using System.Text;
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
public class OverlayFileProvider : IFileProvider, IDisposable
{
    private readonly IFileProvider _innerProvider;
    private readonly PhysicalFileProvider _overlayProvider;
    private readonly bool _ownsOverlayProvider;
    private readonly List<IDisposable> _changeTokenRegistrations = [];

    /// <summary>
    /// Gets the root directory path used for overlay operations.
    /// </summary>
    public string OverlayRoot => _overlayProvider.Root;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayFileProvider"/> class.
    /// Creates a temporary overlay directory for storing modified copies of files.
    /// </summary>
    /// <param name="innerProvider">The underlying file provider to wrap.</param>
    /// <remarks>
    /// The overlay directory is automatically created in the system's temporary folder
    /// and is prefixed with the entry assembly name if available. Files overlayed using
    /// <see cref="CreateOverlay"/> will be physically copied to this temporary location.
    /// </remarks>
    public OverlayFileProvider(IFileProvider innerProvider)
        : this(innerProvider, CreateTempPhysicalFileProvider(), ownsOverlayProvider: true) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayFileProvider" /> class with the specified inner and overlay file providers.
    /// </summary>
    /// <remarks>The overlay provider is typically used to supply files that should take precedence over those
    /// in the inner provider. When resolving files, the overlay provider is checked first, and if a file is not found,
    /// the inner provider is used as a fallback.</remarks>
    /// <param name="innerProvider">The primary file provider that supplies the base set of files. Cannot be null.</param>
    /// <param name="overlayProvider">The file provider whose files will overlay or override those from the inner provider. Cannot be null.</param>
    public OverlayFileProvider(IFileProvider innerProvider, PhysicalFileProvider overlayProvider)
        : this(innerProvider, overlayProvider, ownsOverlayProvider: false) { }

    private OverlayFileProvider(
        IFileProvider innerProvider,
        PhysicalFileProvider overlayProvider,
        bool ownsOverlayProvider
    )
    {
        ArgumentNullException.ThrowIfNull(innerProvider, nameof(innerProvider));
        ArgumentNullException.ThrowIfNull(overlayProvider, nameof(overlayProvider));

        _innerProvider = innerProvider;
        _overlayProvider = overlayProvider;
        _ownsOverlayProvider = ownsOverlayProvider;
    }

    private static PhysicalFileProvider CreateTempPhysicalFileProvider()
    {
        var applicationName = Assembly.GetEntryAssembly()?.GetName().Name;
        var directoryPrefix = applicationName is not null ? applicationName + "-" : null;
        var overlayRoot = Directory.CreateTempSubdirectory(directoryPrefix).FullName;
        return new PhysicalFileProvider(overlayRoot);
    }

    /// <summary>
    /// Creates a physical copy of a file from the inner provider in the overlay directory.
    /// </summary>
    /// <param name="relativeFilePath">The path of the file to overlay, relative to the provider root.</param>
    /// <param name="autoRefresh">If true, automatically re-copies the file when the source changes. Useful for development scenarios with hot-reload.</param>
    /// <param name="preserveLastModifiedTime">If true, preserves the LastModified timestamp from the source file.</param>
    /// <returns>An <see cref="OverlayFile"/> instance that can be used to transform the overlayed file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist in the inner provider.</exception>
    /// <remarks>
    /// <para>Once a file is overlayed, calls to <see cref="GetFileInfo"/> will return the overlay version.</para>
    /// </remarks>
    public OverlayFile CreateOverlay(
        string relativeFilePath,
        bool autoRefresh = false,
        bool preserveLastModifiedTime = true
    )
    {
        var sourceFileInfo = _innerProvider.GetFileInfo(relativeFilePath);
        if (!sourceFileInfo.Exists)
        {
            throw new FileNotFoundException($"Source file not found: {relativeFilePath}");
        }

        var normalizedPath = relativeFilePath.TrimStart('/');
        var destinationPath = Path.Combine(_overlayProvider.Root, normalizedPath);
        var destinationDir = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDir);

        using (var sourceStream = sourceFileInfo.CreateReadStream())
        using (var destStream = File.Create(destinationPath))
        {
            sourceStream.CopyTo(destStream);
        }

        if (preserveLastModifiedTime)
        {
            File.SetLastWriteTimeUtc(destinationPath, sourceFileInfo.LastModified.UtcDateTime);
        }

        var overlayFile = new OverlayFile(
            relativeFilePath,
            destinationPath,
            autoRefresh,
            preserveLastModifiedTime
        );

        if (autoRefresh)
        {
            var registration = ChangeToken.OnChange(
                () => _innerProvider.Watch(relativeFilePath),
                () => OnSourceFileChanged(overlayFile)
            );
            _changeTokenRegistrations.Add(registration);
        }

        return overlayFile;
    }

    private void OnSourceFileChanged(OverlayFile overlayFile)
    {
        try
        {
            var sourceFileInfo = _innerProvider.GetFileInfo(overlayFile.RelativeFilePath);
            if (!sourceFileInfo.Exists)
            {
                return;
            }

            // Reapply all transforms in order
            string content;
            Encoding encoding;
            using (var sourceFile = sourceFileInfo.CreateReadStream())
            using (var reader = new StreamReader(sourceFile))
            {
                content = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }
            foreach (var transform in overlayFile.Transforms)
            {
                content = transform(content);
            }

            var tempFileName = Path.GetTempFileName();
            File.WriteAllText(tempFileName, content, encoding);

            if (overlayFile.PreserveLastModifiedTime)
            {
                File.SetLastWriteTimeUtc(tempFileName, sourceFileInfo.LastModified.UtcDateTime);
            }

            // Replace the existing overlay file (this is atomic on most platforms)
            File.Move(tempFileName, overlayFile.OverlayFilePath, overwrite: true);
        }
        catch
        {
            // Best effort - file might be locked, etc.
        }
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
        var overlayInfo = _overlayProvider.GetFileInfo(subpath);
        return overlayInfo.Exists ? overlayInfo : _innerProvider.GetFileInfo(subpath);
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter) => _innerProvider.Watch(filter);

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        // Dispose all change token registrations
        foreach (var registration in _changeTokenRegistrations)
        {
            registration.Dispose();
        }
        _changeTokenRegistrations.Clear();

        if (_ownsOverlayProvider)
        {
            var overlayRoot = _overlayProvider.Root;
            _overlayProvider.Dispose();

            // Clean up the temporary directory
            if (Directory.Exists(overlayRoot))
            {
                try
                {
                    Directory.Delete(overlayRoot, recursive: true);
                }
                catch
                {
                    // Best effort cleanup - file might be locked
                }
            }
        }
    }
}
