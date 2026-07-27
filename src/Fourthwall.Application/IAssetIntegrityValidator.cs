using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Checks a story's images against its asset store: design doc section 4.2 rule 6.
/// </summary>
/// <remarks>
/// This is separate from <see cref="IStoryValidator"/> because it needs the asset store's I/O, which
/// the structural rules (1–5) deliberately avoid. Both of its findings are warnings — they flag an
/// inconsistency between the story and its files without making the story invalid.
/// </remarks>
public interface IAssetIntegrityValidator
{
    /// <summary>
    /// Validates that every scene image resolves to a stored asset and every asset is referenced.
    /// </summary>
    /// <param name="story">The story whose image references are checked.</param>
    /// <param name="assets">The asset store the story's images live in.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// A report of broken references and orphan assets, both warnings; empty when the story and its
    /// assets agree.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="story"/> or <paramref name="assets"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<ValidationReport> ValidateAsync(
        Story story, IAssetStore assets, CancellationToken cancellationToken = default);
}
