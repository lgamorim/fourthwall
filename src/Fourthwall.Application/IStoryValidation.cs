using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Validates a story completely: its structure and its images, as one report.
/// </summary>
/// <remarks>
/// The rules of design doc section 4.2 are implemented in two halves — structure over the story
/// graph, images against the asset store — because they need different collaborators and one of
/// them reaches the file system. A creator does not care about that split: they want to know
/// whether the story works. This composes the two.
/// </remarks>
public interface IStoryValidation
{
    /// <summary>
    /// Validates a story against every rule.
    /// </summary>
    /// <param name="story">The story to validate.</param>
    /// <param name="assets">The store holding the story's images.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// One report covering every rule, structural violations first, in the order the design states
    /// them.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="story"/> or <paramref name="assets"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<ValidationReport> ValidateAsync(
        Story story, IAssetStore assets, CancellationToken cancellationToken = default);
}
