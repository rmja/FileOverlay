using System.Text;

namespace FileOverlay;

/// <summary>
/// Represents a file that has been overlayed in a temporary directory and can be transformed.
/// </summary>
public class OverlayFile
{
    /// <summary>
    /// Gets the physical path to the overlayed file in the temporary directory.
    /// </summary>
    public string OverlayFilePath { get; }

    /// <summary>
    /// Gets the original file path relative to the provider root.
    /// </summary>
    public string OriginalFilePath { get; }

    internal OverlayFile(string overlayFilePath, string originalFilePath)
    {
        OverlayFilePath = overlayFilePath;
        OriginalFilePath = originalFilePath;
    }

    /// <summary>
    /// Transforms the content of the overlayed file by reading, applying a transformation function,
    /// and writing the result back to the file.
    /// </summary>
    /// <param name="transform">A function that takes the file content as input and returns the transformed content.</param>
    /// <remarks>
    /// The file is read and written using UTF-8 encoding. The transformation is applied in-place,
    /// replacing the file content.
    /// </remarks>
    public void TransformContent(Func<string, string> transform)
    {
        var content = File.ReadAllText(OverlayFilePath, Encoding.UTF8);
        content = transform(content);
        File.WriteAllText(OverlayFilePath, content, Encoding.UTF8);
    }
}
