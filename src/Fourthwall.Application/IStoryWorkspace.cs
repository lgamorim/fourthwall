using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// The story the editor currently has open, and the operations that change which story that is.
/// </summary>
/// <remarks>
/// <see cref="IStoryRepository"/> speaks for one already-open story; this is the layer above it that
/// its documentation defers to — the one that opens and creates the folders in the first place.
/// At most one story is open at a time, matching a tool built for a single creator on a local
/// machine (design doc section 3).
/// <para>
/// Because the open story is shared rather than per-page, components that display it must react to
/// changes they did not cause: <see cref="Changed"/> fires whenever the open story is replaced,
/// closed, or saved.
/// </para>
/// </remarks>
public interface IStoryWorkspace
{
    /// <summary>
    /// Occurs when the open story changes — opened, created, saved, or closed.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets the open story, or <see langword="null"/> when no story is open.
    /// </summary>
    Story? Current { get; }

    /// <summary>
    /// Gets the folder holding the open story, or <see langword="null"/> when no story is open.
    /// </summary>
    string? FolderPath { get; }

    /// <summary>
    /// Creates a story in a new folder and opens it, closing whatever story was open before.
    /// </summary>
    /// <param name="folderPath">The folder to create the story in.</param>
    /// <param name="title">The new story's title.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created story, now open.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="folderPath"/> or <paramref name="title"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="folderPath"/> or <paramref name="title"/> is blank.
    /// </exception>
    /// <exception cref="InvalidOperationException">A story already exists at <paramref name="folderPath"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<Story> CreateAsync(string folderPath, string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an existing story, closing whatever story was open before.
    /// </summary>
    /// <param name="folderPath">The folder holding the story.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The opened story.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="folderPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is blank.</exception>
    /// <exception cref="FileNotFoundException">No story exists at <paramref name="folderPath"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The folder holds a story database with no story in it.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<Story> OpenAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the open story.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the story has been persisted.</returns>
    /// <exception cref="InvalidOperationException">No story is open.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the open story. Closing when no story is open does nothing.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the story has been closed.</returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
