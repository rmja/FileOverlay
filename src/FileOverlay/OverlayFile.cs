using System.Text;

namespace FileOverlay;

/// <summary>
/// Represents a file that has been overlayed in a temporary directory and can be transformed.
/// </summary>
public class OverlayFile
{
    private readonly List<Func<string, string>>? _transforms = [];

    /// <summary>
    /// Gets the path relative to the provider root.
    /// </summary>
    public string RelativeFilePath { get; }

    private readonly bool _autoRefresh;
    private readonly bool _preserveLastModifiedTime;

    /// <summary>
    /// Gets the physical absolute path to the overlayed file in the temporary directory.
    /// </summary>
    public string OverlayFilePath { get; }

    internal IEnumerable<Func<string, string>> Transforms => _transforms ?? [];
    internal bool PreserveLastModifiedTime => _preserveLastModifiedTime;

    internal OverlayFile(
        string relativeFilePath,
        string overlayFilePath,
        bool autoRefresh = false,
        bool preserveLastModifiedTime = true
    )
    {
        OverlayFilePath = overlayFilePath;
        RelativeFilePath = relativeFilePath;
        _autoRefresh = autoRefresh;
        _preserveLastModifiedTime = preserveLastModifiedTime;
        if (autoRefresh)
        {
            _transforms = [];
        }
    }

    /// <summary>
    /// Transforms the content of the overlayed file by reading, applying a transformation function,
    /// and writing the result back to the file.
    /// </summary>
    /// <param name="transform">A function that takes the file content as input and returns the transformed content.</param>
    /// <remarks>
    /// <para>The transformation is applied in-place, replacing the overlay file content.</para>
    /// <para>All transforms are stored and will be reapplied in order when the source file changes (if autoRefresh was enabled).</para>
    /// <para>When autoRefresh is enabled, all previously applied transforms plus the current one will be reapplied whenever the source file changes.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var file = overlay.CreateOverlay("index.html", autoRefresh: true);
    /// file.TransformContent(content => content.Replace("{{TITLE}}", "My App"));
    /// file.TransformContent(content => content.Replace("{{BASE}}", "/myapp/"));
    /// // Both transforms will be reapplied in order when the source changes
    /// </code>
    /// </example>
    public void TransformContent(Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        // Store the transform for potential reapplication
        _transforms?.Add(transform);

        // Apply the transformation
        var overlayFileInfo = new FileInfo(OverlayFilePath);
        var lastModified = overlayFileInfo.LastWriteTimeUtc;

        string content;
        Encoding encoding;
        using (var overlayFile = overlayFileInfo.OpenRead())
        using (var reader = new StreamReader(overlayFile))
        {
            content = reader.ReadToEnd();
            encoding = reader.CurrentEncoding;
        }
        content = transform(content);
        File.WriteAllText(OverlayFilePath, content, encoding);

        if (PreserveLastModifiedTime)
        {
            File.SetLastWriteTimeUtc(OverlayFilePath, lastModified);
        }
    }
}
