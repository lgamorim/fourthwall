namespace Fourthwall.Web;

/// <summary>
/// The image types the editor accepts, and the content types they are served as.
/// </summary>
/// <remarks>
/// One allowlist, used both when ingesting an upload and when serving it back, so the editor
/// cannot accept something it will not render. <see cref="Application.IAssetStore"/> deliberately
/// accepts any safe extension — restricting to images is editor policy, so it lives here.
/// </remarks>
public static class ImageTypes
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
    };

    /// <summary>
    /// Gets the accepted extensions, without leading dots, for showing a creator what to pick.
    /// </summary>
    public static IReadOnlyCollection<string> Extensions => ContentTypes.Keys;

    /// <summary>
    /// Gets the value for a file input's <c>accept</c> attribute.
    /// </summary>
    public static string Accept => string.Join(",", ContentTypes.Keys.Select(extension => $".{extension}"));

    /// <summary>
    /// Determines whether a file name carries an accepted image extension.
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
    /// <returns><see langword="true"/> when the editor accepts it.</returns>
    public static bool IsAccepted(string? fileName) => ContentTypeFor(fileName) is not null;

    /// <summary>
    /// Gets the extension of a file name, without its leading dot.
    /// </summary>
    /// <param name="fileName">The file name to read.</param>
    /// <returns>The extension, lower-cased, or an empty string when there is none.</returns>
    public static string ExtensionOf(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();

    /// <summary>
    /// Gets the content type to serve a file name as.
    /// </summary>
    /// <param name="fileName">The file name, or a story-relative asset path.</param>
    /// <returns>The content type, or <see langword="null"/> when the extension is not accepted.</returns>
    public static string? ContentTypeFor(string? fileName) =>
        ContentTypes.GetValueOrDefault(ExtensionOf(fileName));
}
