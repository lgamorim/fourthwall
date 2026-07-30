using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Holds the story the editor has open, over a <see cref="StoryPackage"/> on disk.
/// </summary>
/// <remarks>
/// One story is open per application instance, not per browser tab: the tool is built for a single
/// creator on a local machine, so two tabs are two views of the same story rather than two
/// independent sessions. Every transition — create, open, save, close — runs under one semaphore so
/// concurrent circuits cannot interleave a save with a reopen.
/// </remarks>
public sealed class StoryPackageWorkspace : IStoryWorkspace, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private StoryPackage? _package;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public Story? Current { get; private set; }

    /// <inheritdoc/>
    public string? FolderPath { get; private set; }

    /// <inheritdoc/>
    public async Task<Story> CreateAsync(
        string folderPath, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return await SwitchToAsync(
            folderPath,
            async ct =>
            {
                var package = await StoryPackage.CreateAsync(folderPath, ct).ConfigureAwait(false);
                try
                {
                    // Save immediately: a folder holding a database with no story in it is a folder
                    // this workspace refuses to open.
                    var story = new Story(title);
                    await package.Repository.SaveAsync(story, ct).ConfigureAwait(false);
                    return (package, story);
                }
                catch
                {
                    await package.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Story> OpenAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        return await SwitchToAsync(
            folderPath,
            async ct =>
            {
                var package = await StoryPackage.OpenAsync(folderPath, ct).ConfigureAwait(false);
                try
                {
                    var story = await package.Repository.LoadAsync(ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException(
                            $"The story at '{folderPath}' is empty: its database holds no story.");
                    return (package, story);
                }
                catch
                {
                    await package.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_package is null || Current is null)
            {
                throw new InvalidOperationException("No story is open.");
            }

            await _package.Repository.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        OnChanged();
    }

    /// <inheritdoc/>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool closed;
        try
        {
            closed = await ReleaseAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        if (closed)
        {
            OnChanged();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task<Story> SwitchToAsync(
        string folderPath,
        Func<CancellationToken, Task<(StoryPackage Package, Story Story)>> openAsync,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = await openAsync(cancellationToken).ConfigureAwait(false);

            // Only release the story that was open once the new one is in hand, so a failed open
            // leaves the creator with the story they had.
            await ReleaseAsync().ConfigureAwait(false);

            _package = opened.Package;
            Current = opened.Story;
            FolderPath = folderPath;
        }
        finally
        {
            _gate.Release();
        }

        OnChanged();
        return Current;
    }

    private async Task<bool> ReleaseAsync()
    {
        if (_package is null)
        {
            return false;
        }

        await _package.DisposeAsync().ConfigureAwait(false);
        _package = null;
        Current = null;
        FolderPath = null;
        return true;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
