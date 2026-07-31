using System.Text.Json;
using Fourthwall.Application;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Remembers recently opened stories in a JSON file outside every story folder.
/// </summary>
/// <remarks>
/// The list spans folders, so it belongs to the editor rather than to any one story — typically a
/// file under the user's application data. JSON rather than SQLite: the list is a handful of
/// entries with no schema to evolve, and the migration machinery a database would bring costs more
/// than it is worth here.
/// <para>
/// Like the workspace, this is a shared singleton, and recording is a read-modify-write over one
/// file: every operation runs under one semaphore so two circuits cannot lose each other's updates.
/// </para>
/// </remarks>
public sealed class JsonRecentStoriesStore : IRecentStories
{
    private const int MaximumEntries = 10;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    // Never disposed, and does not need to be: nothing here touches AvailableWaitHandle, so the
    // semaphore holds no unmanaged resource.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly string _filePath;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a store that stamps entries with the system clock.
    /// </summary>
    /// <param name="filePath">The JSON file holding the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is blank.</exception>
    public JsonRecentStoriesStore(string filePath)
        : this(filePath, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a store that stamps entries with the given clock.
    /// </summary>
    /// <param name="filePath">The JSON file holding the list.</param>
    /// <param name="timeProvider">Supplies the moment each entry was last opened.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filePath"/> or <paramref name="timeProvider"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is blank.</exception>
    public JsonRecentStoriesStore(string filePath, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _filePath = filePath;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecentStory>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return entries.Select(entry => entry.ToRecentStory()).ToList();
        }
        catch (IOException)
        {
            // Listing runs as the picker initializes, with nothing above it to catch a failure. An
            // unreadable file shows an empty list rather than breaking the page.
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RecordAsync(
        string folderPath, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // An IOException is deliberately not swallowed here: writing over a list we failed to
            // read would discard it. Better to tell the creator than to silently reset.
            var entries = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var kept = entries.Where(entry => !SameFolder(entry, folderPath));
            var recorded = new Entry(title, folderPath, _timeProvider.GetUtcNow());

            await WriteAsync([recorded, .. kept.Take(MaximumEntries - 1)], cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var kept = entries.Where(entry => !SameFolder(entry, folderPath)).ToList();
            if (kept.Count != entries.Count)
            {
                await WriteAsync(kept, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // A hand-edited file can hold entries the ports would reject; drop them rather than fail.
    private static bool IsUsable(Entry entry) =>
        !string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrWhiteSpace(entry.FolderPath);

    // Ordinal, matching FileSystemAssetStore's deliberate choice: two spellings of one path are two
    // entries, which is easier to live with than a comparison that guesses at the file system.
    private static bool SameFolder(Entry entry, string folderPath) =>
        string.Equals(entry.FolderPath, folderPath, StringComparison.Ordinal);

    private async Task<IReadOnlyList<Entry>> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var entries = await JsonSerializer
                .DeserializeAsync<List<Entry>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return entries?.Where(IsUsable).ToList() ?? [];
        }
        catch (JsonException)
        {
            // Damaged content is unrecoverable and starts over. An IOException is different — the
            // file may be intact and momentarily unreachable — so it is left to the caller, which
            // knows whether it is about to overwrite what it could not read.
            return [];
        }
    }

    private async Task WriteAsync(IReadOnlyList<Entry> entries, CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    // Carries the persisted fields and nothing else: every property here becomes a property in the
    // file, which a person may read and edit.
    private sealed record Entry(string Title, string FolderPath, DateTimeOffset LastOpenedUtc)
    {
        public RecentStory ToRecentStory() => new(Title, FolderPath, LastOpenedUtc);
    }
}
