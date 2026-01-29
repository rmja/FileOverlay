using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace FileOverlay;

/// <summary>
/// Extension methods for rewriting HTML base href attributes.
/// </summary>
public static partial class BaseHrefExtensions
{
    [GeneratedRegex(@"<base\s+href\s*=\s*""([^""]*)""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BaseHrefRegex();

    /// <summary>
    /// Creates an overlay file provider that rewrites the base href attribute in specified HTML files.
    /// </summary>
    /// <param name="fileProvider">The file provider to wrap.</param>
    /// <param name="pathBase">The path base to use in the base href attribute. If null or empty, files are copied but not modified.</param>
    /// <param name="filePaths">The paths of HTML files to rewrite, relative to the provider root (e.g., "index.html").</param>
    /// <returns>An <see cref="OverlayFileProvider"/> with the specified files overlayed and transformed.</returns>
    /// <remarks>
    /// <para>
    /// This method is useful for ASP.NET Core applications hosted under a path base (e.g., "/myapp")
    /// where the HTML base href needs to be dynamically rewritten to match the deployment path.
    /// </para>
    /// <para>
    /// The pathBase will automatically have a trailing slash appended if not present.
    /// The regex matches base tags like: &lt;base href="/" /&gt; or &lt;base href="/"&gt;
    /// </para>
    /// </remarks>
    public static OverlayFileProvider WithBaseHrefRewrite(
        this IFileProvider fileProvider,
        string pathBase,
        params string[] filePaths
    )
    {
        var overlay = new OverlayFileProvider(fileProvider);

        foreach (var filePath in filePaths)
        {
            var file = overlay.CreateOverlay(filePath);
            file.TransformContent(content => RewriteBaseHref(content, pathBase));
        }

        return overlay;
    }

    /// <summary>
    /// Creates an overlay file provider that rewrites the base href attribute in specified HTML files.
    /// </summary>
    /// <param name="fileProvider">The file provider to wrap.</param>
    /// <param name="pathBase">The path base to use in the base href attribute. If null or empty, files are copied but not modified.</param>
    /// <param name="autoRefresh">If true, automatically re-copies and transforms files when the source changes. Useful for development scenarios with hot-reload.</param>
    /// <param name="filePaths">The paths of HTML files to rewrite, relative to the provider root (e.g., "index.html").</param>
    /// <returns>An <see cref="OverlayFileProvider"/> with the specified files overlayed and transformed.</returns>
    /// <remarks>
    /// <para>
    /// This method is useful for ASP.NET Core applications hosted under a path base (e.g., "/myapp")
    /// where the HTML base href needs to be dynamically rewritten to match the deployment path.
    /// </para>
    /// <para>
    /// The pathBase will automatically have a trailing slash appended if not present.
    /// The regex matches base tags like: &lt;base href="/" /&gt; or &lt;base href="/"&gt;
    /// </para>
    /// <para>
    /// When autoRefresh is enabled, files will be re-copied and transformations reapplied when source files change.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Support hot-reload in development
    /// var fileProvider = env.WebRootFileProvider.WithBaseHrefRewrite(
    ///     pathBase: "/myapp",
    ///     autoRefresh: app.Environment.IsDevelopment(),
    ///     "index.html"
    /// );
    ///
    /// app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
    /// </code>
    /// </example>
    public static OverlayFileProvider WithBaseHrefRewrite(
        this IFileProvider fileProvider,
        string pathBase,
        bool autoRefresh,
        params string[] filePaths
    )
    {
        var overlay = new OverlayFileProvider(fileProvider);

        foreach (var filePath in filePaths)
        {
            var file = overlay.CreateOverlay(filePath, autoRefresh);
            file.TransformContent(content => RewriteBaseHref(content, pathBase));
        }

        return overlay;
    }

    private static string RewriteBaseHref(string content, string pathBase)
    {
        if (string.IsNullOrEmpty(pathBase))
        {
            return content;
        }

        if (!pathBase.EndsWith('/'))
        {
            pathBase += "/";
        }

        return BaseHrefRegex().Replace(content, $"<base href=\"{pathBase}\" />");
    }
}
