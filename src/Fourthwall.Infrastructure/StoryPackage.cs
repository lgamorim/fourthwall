using System.Data.Common;
using Fourthwall.Application;

namespace Fourthwall.Infrastructure;

/// <summary>
/// An open story on disk: the folder holding <c>story.db</c> and its <c>assets/</c> images, exposed
/// as a bound <see cref="IStoryRepository"/> and <see cref="IAssetStore"/>.
/// </summary>
/// <remarks>
/// A story is created or opened through <see cref="CreateAsync"/> / <see cref="OpenAsync"/>, which
/// wire the connection, schema migration, repository, and asset store for one folder. The package
/// owns the database connection — the repository only borrows it — so disposing the package closes
/// the story.
/// </remarks>
public sealed class StoryPackage : IAsyncDisposable
{
    private const string DatabaseFileName = "story.db";
    private const string AssetsFolderName = "assets";
    private readonly DbConnection _connection;

    private StoryPackage(DbConnection connection, IStoryRepository repository, IAssetStore assets)
    {
        _connection = connection;
        Repository = repository;
        Assets = assets;
    }

    /// <summary>
    /// Gets the repository that saves and loads this story.
    /// </summary>
    public IStoryRepository Repository { get; }

    /// <summary>
    /// Gets the asset store that ingests and resolves this story's images.
    /// </summary>
    public IAssetStore Assets { get; }

    /// <summary>
    /// Creates a new story folder — its <c>story.db</c> (migrated to the current schema) and
    /// <c>assets/</c> directory — and opens it.
    /// </summary>
    /// <param name="folderPath">The folder to create the story in.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The opened story package.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="folderPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">A story already exists at <paramref name="folderPath"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public static async Task<StoryPackage> CreateAsync(
        string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        cancellationToken.ThrowIfCancellationRequested();

        var databasePath = Path.Combine(folderPath, DatabaseFileName);
        if (File.Exists(databasePath))
        {
            throw new InvalidOperationException($"A story already exists at '{folderPath}'.");
        }

        Directory.CreateDirectory(folderPath);
        Directory.CreateDirectory(Path.Combine(folderPath, AssetsFolderName));

        return await OpenMigratedAsync(folderPath, databasePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens an existing story folder, migrating its <c>story.db</c> to the current schema.
    /// </summary>
    /// <param name="folderPath">The folder holding the story.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The opened story package.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="folderPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is blank.</exception>
    /// <exception cref="FileNotFoundException">No <c>story.db</c> exists at <paramref name="folderPath"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public static async Task<StoryPackage> OpenAsync(
        string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        cancellationToken.ThrowIfCancellationRequested();

        var databasePath = Path.Combine(folderPath, DatabaseFileName);
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException($"No story exists at '{folderPath}'.", databasePath);
        }

        return await OpenMigratedAsync(folderPath, databasePath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private static async Task<StoryPackage> OpenMigratedAsync(
        string folderPath, string databasePath, CancellationToken cancellationToken)
    {
        var connection = await new SqliteConnectionFactory().OpenAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
            var repository = new SqliteStoryRepository(connection);
            var assets = new FileSystemAssetStore(folderPath);
            return new StoryPackage(connection, repository, assets);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
