using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// An in-memory <see cref="IStoryWorkspace"/> standing in for real story folders. Folders exist
/// only in <see cref="Stories"/>, so a folder is "missing" exactly when it was never added.
/// </summary>
public sealed class FakeStoryWorkspace : IStoryWorkspace
{
    public event EventHandler? Changed;

    public Dictionary<string, Story> Stories { get; } = [];

    public Story? Current { get; private set; }

    public string? FolderPath { get; private set; }

    public int SaveCount { get; private set; }

    /// <summary>
    /// When set, the next save throws this instead of succeeding, and the failure is cleared.
    /// </summary>
    public Exception? FailNextSave { get; set; }

    public Task<Story> CreateAsync(
        string folderPath, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (Stories.ContainsKey(folderPath))
        {
            throw new InvalidOperationException($"A story already exists at '{folderPath}'.");
        }

        var story = new Story(title);
        Stories[folderPath] = story;
        return Task.FromResult(Switch(folderPath, story));
    }

    public Task<Story> OpenAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Stories.TryGetValue(folderPath, out var story))
        {
            throw new FileNotFoundException($"No story exists at '{folderPath}'.");
        }

        return Task.FromResult(Switch(folderPath, story));
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            throw new InvalidOperationException("No story is open.");
        }

        if (FailNextSave is not null)
        {
            var failure = FailNextSave;
            FailNextSave = null;
            throw failure;
        }

        SaveCount++;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Current is null)
        {
            return Task.CompletedTask;
        }

        Current = null;
        FolderPath = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Story Switch(string folderPath, Story story)
    {
        Current = story;
        FolderPath = folderPath;
        Changed?.Invoke(this, EventArgs.Empty);
        return story;
    }
}
