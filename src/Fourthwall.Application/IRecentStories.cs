namespace Fourthwall.Application;

/// <summary>
/// Remembers which story folders were opened, so a creator can return to one without retyping
/// its path.
/// </summary>
/// <remarks>
/// This is editor state, not story data: it spans folders and therefore cannot live inside any one
/// of them (design doc section 4.3 makes the folder the unit of distribution). The list is short
/// and ordered by recency; implementations decide where it is kept.
/// </remarks>
public interface IRecentStories
{
    /// <summary>
    /// Lists the remembered stories, most recently opened first.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The remembered stories, or an empty list when none are remembered.</returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<IReadOnlyList<RecentStory>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a story as the most recently opened one, replacing any earlier entry for the same
    /// folder.
    /// </summary>
    /// <param name="folderPath">The folder holding the story.</param>
    /// <param name="title">The story's title, so the list can name it without opening it.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the story has been recorded.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="folderPath"/> or <paramref name="title"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="folderPath"/> or <paramref name="title"/> is blank.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task RecordAsync(string folderPath, string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets a story, whether because its folder is gone or because the creator asked.
    /// </summary>
    /// <param name="folderPath">The folder to forget. Forgetting an unknown folder does nothing.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the story has been forgotten.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="folderPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is blank.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task RemoveAsync(string folderPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// A story the editor remembers opening.
/// </summary>
public sealed record RecentStory
{
    /// <summary>
    /// Initializes a new remembered story.
    /// </summary>
    /// <param name="title">The story's title.</param>
    /// <param name="folderPath">The folder holding the story.</param>
    /// <param name="lastOpenedUtc">When the story was last opened.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="title"/> or <paramref name="folderPath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="title"/> or <paramref name="folderPath"/> is blank.
    /// </exception>
    public RecentStory(string title, string folderPath, DateTimeOffset lastOpenedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        Title = title;
        FolderPath = folderPath;
        LastOpenedUtc = lastOpenedUtc;
    }

    /// <summary>
    /// Gets the story's title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the folder holding the story.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// Gets the moment the story was last opened.
    /// </summary>
    public DateTimeOffset LastOpenedUtc { get; }
}
