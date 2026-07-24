using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class StoryDatabaseMigratorTests : IDisposable
{
    private readonly string _databaseDirectory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory = new();

    public StoryDatabaseMigratorTests()
    {
        _databaseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_databaseDirectory);
        _databasePath = Path.Combine(_databaseDirectory, "story.db");
    }

    [Fact]
    public async Task Should_CreateEveryTable_When_Migrated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(cancellationToken);

        await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken);

        var tables = await TableNamesAsync(connection, cancellationToken);
        foreach (var expected in new[] { "scenes", "stories", "choices", "editor_scene_layout", "_sqlbound_migrations" })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task Should_GiveScenesTheExpectedColumns_When_Migrated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(cancellationToken);

        await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken);

        var columns = await ColumnNamesAsync(connection, "scenes", cancellationToken);
        string[] expectedColumns =
        [
            "id", "kind", "text", "image_path", "follow_up_scene_id",
            "outcome_kind", "outcome_label", "extension_tag", "extension_payload",
        ];
        foreach (var expected in expectedColumns)
        {
            Assert.Contains(expected, columns);
        }
    }

    [Fact]
    public async Task Should_RecordOneAppliedMigration_When_Migrated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var appliedAt = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        await using var connection = await OpenAsync(cancellationToken);
        var migrator = new StoryDatabaseMigrator(new FixedTimeProvider(appliedAt));

        var applied = await migrator.MigrateAsync(connection, cancellationToken);

        Assert.Equal(1, applied);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT checksum, applied_on_utc FROM _sqlbound_migrations;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(0)));
        Assert.Equal(appliedAt.UtcDateTime, reader.GetDateTime(1), TimeSpan.FromSeconds(1));
        Assert.False(await reader.ReadAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_ApplyNothing_When_MigratedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(cancellationToken);
        var migrator = new StoryDatabaseMigrator();

        var first = await migrator.MigrateAsync(connection, cancellationToken);
        var second = await migrator.MigrateAsync(connection, cancellationToken);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, await LedgerCountAsync(connection, cancellationToken));
    }

    [Fact]
    public async Task Should_EnableForeignKeys_When_ConnectionOpened()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        var enabled = await command.ExecuteScalarAsync(cancellationToken);

        Assert.Equal(1L, Convert.ToInt64(enabled));
    }

    [Fact]
    public async Task Should_DropEveryTable_When_DownScriptRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(cancellationToken);
        await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken);

        var downScript = await File.ReadAllTextAsync(DownScriptPath(), cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = downScript;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var tables = await TableNamesAsync(connection, cancellationToken);
        Assert.DoesNotContain("scenes", tables);
        Assert.DoesNotContain("stories", tables);
        Assert.DoesNotContain("choices", tables);
        Assert.DoesNotContain("editor_scene_layout", tables);
    }

    [Fact]
    public async Task Should_AllowCyclicFollowUps_When_WrittenInOneTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(cancellationToken);
        await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken);

        // Two Linear scenes whose follow-ups point at each other — a legal cycle (design doc 4.2).
        // Immediate foreign keys have no valid insert order here; the deferred declaration lets both
        // rows be written before either target exists, with the check running at commit.
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await InsertLinearSceneAsync(connection, transaction, "a", followUp: "b", cancellationToken);
            await InsertLinearSceneAsync(connection, transaction, "b", followUp: "a", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM scenes;";
        Assert.Equal(2, Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)));
    }

    [Fact]
    public void Should_Throw_When_TimeProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StoryDatabaseMigrator(null!));
    }

    [Fact]
    public async Task Should_Throw_When_ConnectionIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new StoryDatabaseMigrator().MigrateAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_MigrateIsAlreadyCancelled()
    {
        await using var connection = await OpenAsync(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // The async ADO.NET path cancels via Task.FromCanceled, which surfaces the
        // OperationCanceledException-derived TaskCanceledException.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new StoryDatabaseMigrator().MigrateAsync(connection, cancelled.Token));
    }

    public void Dispose()
    {
        // Release the pooled file handle so the temp database file can be deleted on Windows.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle during teardown must not fail the test; the temp folder is disposable.
        }
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        await _connectionFactory.OpenAsync(_databasePath, cancellationToken);

    private static string DownScriptPath() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Migrations"), "*.down.sql").Single();

    private static async Task InsertLinearSceneAsync(
        DbConnection connection,
        DbTransaction transaction,
        string id,
        string followUp,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO scenes (id, kind, text, follow_up_scene_id) VALUES (@id, 'Linear', '', @followUp);";
        AddParameter(command, "@id", id);
        AddParameter(command, "@followUp", followUp);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<int> LedgerCountAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM _sqlbound_migrations;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<IReadOnlySet<string>> TableNamesAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        return await ReadStringsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlySet<string>> ColumnNamesAsync(
        DbConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_info('{table}');";
        return await ReadStringsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlySet<string>> ReadStringsAsync(
        DbCommand command, CancellationToken cancellationToken)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
