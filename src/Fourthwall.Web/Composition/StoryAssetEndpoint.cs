using Fourthwall.Application;

namespace Fourthwall.Web.Composition;

/// <summary>
/// Serves the open story's scene images, so a page can render an asset the story folder holds.
/// </summary>
/// <remarks>
/// The path is the story-relative path a scene recorded, handed to the asset store unchanged — the
/// endpoint never learns where the story folder is or how it is laid out. Containment is the
/// store's job and stays there.
/// </remarks>
public static class StoryAssetEndpoint
{
    /// <summary>The route the story's assets are served from.</summary>
    public const string Route = "/story-asset/{**path}";

    /// <summary>The prefix a page uses to build an asset URL.</summary>
    public const string UrlPrefix = "/story-asset/";

    /// <summary>
    /// Serves one asset of the open story.
    /// </summary>
    /// <param name="workspace">The workspace holding the open story.</param>
    /// <param name="path">The story-relative path of the asset.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The asset, or a not-found result.</returns>
    public static async Task<IResult> HandleAsync(
        IStoryWorkspace workspace, string? path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        // No story open, nothing named, or a type this editor does not serve. A hand-edited scene
        // path must not turn this into a general reader for the story folder, so the extension is
        // checked before the store is asked for anything.
        if (workspace.Assets is not { } assets
            || string.IsNullOrWhiteSpace(path)
            || ImageTypes.ContentTypeFor(path) is not { } contentType)
        {
            return TypedResults.NotFound();
        }

        var stream = await assets.OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            // A scene may reference an asset that is gone; validation reports that as a warning.
            return TypedResults.NotFound();
        }

        return new ImmutableAsset(stream, contentType);
    }

    // Asset names are content hashes, so the bytes behind a name never change and the response can
    // be cached indefinitely. The header belongs to the success path only: caching a not-found
    // would outlive the asset appearing later.
    private sealed class ImmutableAsset(Stream stream, string contentType) : IResult, IContentTypeHttpResult
    {
        public string? ContentType => contentType;

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            await TypedResults.File(stream, contentType).ExecuteAsync(httpContext).ConfigureAwait(false);
        }
    }
}
