using System.Data.Common;
using SqlBound.Migrations;
using SqlBound.Sqlite;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Brings a story's SQLite database up to the current schema by applying the migrations that ship
/// with this assembly.
/// </summary>
/// <remarks>
/// The migration SQL files are copied beside the assembly (see the project's <c>Content</c> items)
/// and read from disk at runtime by SqlBound. Applying is idempotent — already-applied migrations
/// are skipped — so it is safe to call every time a story is opened.
/// </remarks>
public sealed class StoryDatabaseMigrator
{
    private const string MigrationsFolderName = "Migrations";
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a migrator that stamps applied migrations with the system clock.
    /// </summary>
    public StoryDatabaseMigrator()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a migrator that stamps applied migrations with the given clock.
    /// </summary>
    /// <param name="timeProvider">Supplies the timestamp recorded for each applied migration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public StoryDatabaseMigrator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Applies every pending migration to the database behind the given open connection.
    /// </summary>
    /// <param name="connection">An open connection to the story database.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The number of migrations applied by this call; zero when already up to date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<int> MigrateAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var migrations = MigrationDirectory.Load(ResolveMigrationsDirectory());
        var applied = await MigrationRunner
            .RunAsync(connection, new SqliteMigrationLedger(), migrations, _timeProvider, cancellationToken)
            .ConfigureAwait(false);

        return applied.Count;
    }

    // Isolated so that a future single-file publish can switch to embedded resources extracted to a
    // temp directory without touching the rest of the migrator.
    private static string ResolveMigrationsDirectory() =>
        Path.Combine(AppContext.BaseDirectory, MigrationsFolderName);
}
