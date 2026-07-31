using Fourthwall.Application;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// An in-memory <see cref="IRecentStories"/> keeping the real store's ordering and dedupe rules,
/// with a counter standing in for the clock so recorded order is deterministic.
/// </summary>
public sealed class FakeRecentStories : IRecentStories
{
    private static readonly DateTimeOffset Epoch = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly List<RecentStory> _stories = [];
    private int _recorded;

    public Task<IReadOnlyList<RecentStory>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecentStory>>([.. _stories]);

    public Task RecordAsync(
        string folderPath, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        _stories.RemoveAll(story => story.FolderPath == folderPath);
        _stories.Insert(0, new RecentStory(title, folderPath, Epoch.AddMinutes(_recorded++)));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        _stories.RemoveAll(story => story.FolderPath == folderPath);
        return Task.CompletedTask;
    }
}
