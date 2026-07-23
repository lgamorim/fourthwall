using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Persists a story and reads it back.
/// </summary>
/// <remarks>
/// An instance is bound to a single open story. A story on disk is a folder holding exactly one
/// database with one story (design doc section 4.3), so these methods carry no story identifier
/// or location: the repository already knows which story it speaks for. The mechanism that opens
/// or creates that folder, and any catalogue of stories across folders, live above this port.
/// <para>
/// Both operations are asynchronous because the implementations that matter cross to a real
/// database, and both honour their <see cref="CancellationToken"/> so a slow save or load can be
/// abandoned.
/// </para>
/// </remarks>
public interface IStoryRepository
{
    /// <summary>
    /// Persists a story, replacing whatever was stored before.
    /// </summary>
    /// <param name="story">The story to persist.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the story has been persisted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="story"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task SaveAsync(Story story, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the persisted story.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The persisted story, or <see langword="null"/> when nothing has been saved yet.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<Story?> LoadAsync(CancellationToken cancellationToken = default);
}
