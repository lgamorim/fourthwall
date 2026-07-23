namespace Fourthwall.Application;

/// <summary>
/// Ingests scene images into a story's asset folder and resolves them afterwards.
/// </summary>
/// <remarks>
/// A story keeps its images as files under an <c>assets/</c> folder and stores only their
/// story-relative paths on the scenes that use them (design doc decision D5). This port copies an
/// image in under a content-derived name — so identical images collapse to one file and a changed
/// image never masquerades under an old name — and answers the questions asset-integrity
/// validation asks: does a referenced asset exist, and which assets exist at all.
/// <para>
/// Every operation is asynchronous and honours its <see cref="CancellationToken"/>, because the
/// implementations that matter read and write real files.
/// </para>
/// </remarks>
public interface IAssetStore
{
    /// <summary>
    /// Copies an image into the story's asset folder under a content-derived name.
    /// </summary>
    /// <param name="content">The image bytes to ingest.</param>
    /// <param name="fileExtension">
    /// The image's file extension, without a leading dot and containing no dot or path separator,
    /// such as <c>png</c>. It is matched case-insensitively and stored lower-cased, so the name it
    /// produces can be served with the right type later. Rejecting dots and separators keeps the
    /// extension from widening the stored path into a traversal.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The story-relative path of the stored asset, such as <c>assets/&lt;hash&gt;.png</c>, to
    /// record on the scene that references it. Ingesting identical content under an equivalent
    /// extension returns the same path.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content"/> or <paramref name="fileExtension"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fileExtension"/> is blank, or contains a dot or a path separator.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<string> IngestAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a stored asset resolves to an existing file.
    /// </summary>
    /// <param name="relativePath">The story-relative path to check.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the asset exists; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is blank.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the story-relative paths of every stored asset.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The story-relative path of each stored asset, which may be empty.</returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<IReadOnlyCollection<string>> ListAsync(CancellationToken cancellationToken = default);
}
