using System.Data.Common;
using Fourthwall.Application;
using Fourthwall.Domain;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class SqliteSceneLayoutStoreTests : IDisposable
{
    private readonly string _databaseDirectory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory = new();

    public SqliteSceneLayoutStoreTests()
    {
        _databaseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_databaseDirectory);
        _databasePath = Path.Combine(_databaseDirectory, "story.db");
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NothingSaved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var store = new SqliteSceneLayoutStore(connection);

        var positions = await store.LoadAsync(cancellationToken);

        Assert.Empty(positions);
    }

    [Fact]
    public async Task Should_RoundTripPositions_When_SavedAndLoaded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Layout");
        var first = story.AddScene(SceneKind.Linear, "first");
        var second = story.AddScene(SceneKind.Linear, "second");
        await SaveStoryAsync(story, cancellationToken);

        await using (var connection = await OpenMigratedAsync(cancellationToken))
        {
            await new SqliteSceneLayoutStore(connection).SaveAsync(
                new Dictionary<SceneId, ScenePosition>
                {
                    [first.Id] = new(12.5, -30),
                    [second.Id] = new(0, 4),
                },
                cancellationToken);
        }

        // A fresh connection proves the positions survive to disk, not just in the writer's session.
        await using var reopened = await OpenMigratedAsync(cancellationToken);
        var positions = await new SqliteSceneLayoutStore(reopened).LoadAsync(cancellationToken);

        Assert.Equal(2, positions.Count);
        Assert.Equal(new ScenePosition(12.5, -30), positions[first.Id]);
        Assert.Equal(new ScenePosition(0, 4), positions[second.Id]);
    }

    [Fact]
    public async Task Should_OverwritePosition_When_SavedAgainForSameScene()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Layout");
        var scene = story.AddScene(SceneKind.Linear, "dragged");
        await SaveStoryAsync(story, cancellationToken);
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var store = new SqliteSceneLayoutStore(connection);
        await store.SaveAsync(
            new Dictionary<SceneId, ScenePosition> { [scene.Id] = new(1, 2) }, cancellationToken);

        await store.SaveAsync(
            new Dictionary<SceneId, ScenePosition> { [scene.Id] = new(30, 40) }, cancellationToken);

        var positions = await store.LoadAsync(cancellationToken);
        Assert.Equal(new ScenePosition(30, 40), Assert.Single(positions).Value);
    }

    [Fact]
    public async Task Should_SaveNothing_When_OneSceneIsUnknown()
    {
        // The batch is one transaction, so a position for a scene that was never saved rolls the
        // whole call back rather than leaving half a layout behind.
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Layout");
        var known = story.AddScene(SceneKind.Linear, "known");
        await SaveStoryAsync(story, cancellationToken);
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var store = new SqliteSceneLayoutStore(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(
                new Dictionary<SceneId, ScenePosition>
                {
                    [known.Id] = new(1, 2),
                    [SceneId.New()] = new(3, 4),
                },
                cancellationToken));

        Assert.Empty(await store.LoadAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_ReportTheFailure_When_TheDatabaseIsLocked()
    {
        // Another process holding the database (a backup tool, a second editor) fails the save at
        // the transaction's start, before any statement runs. That failure has to reach the
        // creator as the adapter's own exception, like every other database failure, rather than
        // escape as the provider's.
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Layout");
        var scene = story.AddScene(SceneKind.Linear, "held");
        await SaveStoryAsync(story, cancellationToken);
        await using var connection = await OpenMigratedAsync(cancellationToken);
        ((SqliteConnection)connection).DefaultTimeout = 1;
        var store = new SqliteSceneLayoutStore(connection);
        await using var holder = new SqliteConnection($"Data Source={_databasePath}");
        await holder.OpenAsync(cancellationToken);
        await using var hold = holder.CreateCommand();
        hold.CommandText = "BEGIN EXCLUSIVE";
        await hold.ExecuteNonQueryAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(
                new Dictionary<SceneId, ScenePosition> { [scene.Id] = new(1, 2) }, cancellationToken));

        Assert.IsType<SqliteException>(exception.InnerException);
    }

    [Fact]
    public async Task Should_BlameTheUnsavedScene_When_SaveViolatesTheForeignKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SaveStoryAsync(new Story("Layout"), cancellationToken);
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var store = new SqliteSceneLayoutStore(connection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(
                new Dictionary<SceneId, ScenePosition> { [SceneId.New()] = new(1, 2) }, cancellationToken));

        Assert.Contains("scene", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_NotBlameTheScene_When_SaveFailsForAnotherReason()
    {
        // A failure that is not the foreign key — here a read-only database, but equally a locked
        // one or a full disk — must not be reported as a position naming a scene that does not
        // exist, which would send the creator looking for a story problem that is not there.
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Layout");
        var scene = story.AddScene(SceneKind.Linear, "a");
        await SaveStoryAsync(story, cancellationToken);

        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
        await connection.OpenAsync(cancellationToken);
        var store = new SqliteSceneLayoutStore(connection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(
                new Dictionary<SceneId, ScenePosition> { [scene.Id] = new(1, 2) }, cancellationToken));

        Assert.DoesNotContain("scene", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_Throw_When_SaveIsAlreadyCancelled()
    {
        await using var connection = await OpenMigratedAsync(TestContext.Current.CancellationToken);
        var store = new SqliteSceneLayoutStore(connection);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(
                new Dictionary<SceneId, ScenePosition> { [SceneId.New()] = new(1, 2) }, cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_SavingNullPositions()
    {
        await using var connection = await OpenMigratedAsync(TestContext.Current.CancellationToken);
        var store = new SqliteSceneLayoutStore(connection);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Throw_When_ConnectionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SqliteSceneLayoutStore(null!));
    }

    public void Dispose()
    {
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

    private async Task SaveStoryAsync(Story story, CancellationToken cancellationToken)
    {
        // editor_scene_layout references scenes, so the scenes have to exist before a position can.
        await using var connection = await OpenMigratedAsync(cancellationToken);
        await new SqliteStoryRepository(connection).SaveAsync(story, cancellationToken);
    }

    private async Task<DbConnection> OpenMigratedAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.OpenAsync(_databasePath, cancellationToken);
        await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken);
        return connection;
    }
}
