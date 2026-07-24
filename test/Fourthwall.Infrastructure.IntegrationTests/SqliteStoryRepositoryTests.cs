using System.Data.Common;
using Fourthwall.Domain;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class SqliteStoryRepositoryTests : IDisposable
{
    private readonly string _databaseDirectory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory = new();

    public SqliteStoryRepositoryTests()
    {
        _databaseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_databaseDirectory);
        _databasePath = Path.Combine(_databaseDirectory, "story.db");
    }

    [Fact]
    public async Task Should_RoundTripEveryDetail_When_StorySavedAndReloaded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = BuildRichStory();

        await SaveAsync(story, cancellationToken);
        var loaded = await LoadAsync(cancellationToken);

        Assert.NotNull(loaded);
        AssertSameStory(story, loaded);
    }

    [Fact]
    public async Task Should_ReturnNull_When_DatabaseHasNoStory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var loaded = await LoadAsync(cancellationToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Should_ReplacePreviousStory_When_SavedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = new Story("First");
        first.AddScene(SceneKind.Linear, "only");
        var second = new Story("Second");
        var start = second.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        second.SetStartScene(start.Id);

        await SaveAsync(first, cancellationToken);
        await SaveAsync(second, cancellationToken);
        var loaded = await LoadAsync(cancellationToken);

        Assert.NotNull(loaded);
        AssertSameStory(second, loaded);
    }

    [Fact]
    public async Task Should_PreserveChoiceOrder_When_SceneHasMultipleChoices()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Choices");
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        var c = story.AddScene(SceneKind.Linear, "c");
        story.WireChoice(fork.Id, "first", a.Id);
        story.WireChoice(fork.Id, "second", b.Id);
        story.WireChoice(fork.Id, "third", c.Id);

        await SaveAsync(story, cancellationToken);
        var loaded = await LoadAsync(cancellationToken);

        var reloadedFork = loaded!.FindScene(fork.Id);
        Assert.NotNull(reloadedFork);
        Assert.Collection(
            reloadedFork.Choices,
            choice => Assert.Equal("first", choice.Label),
            choice => Assert.Equal("second", choice.Label),
            choice => Assert.Equal("third", choice.Label));
    }

    [Fact]
    public async Task Should_RoundTripCyclicFollowUps_When_ScenesReferenceEachOther()
    {
        // Two Linear scenes flowing into each other — a legal cycle with no valid single-row
        // insert order; proves the repository writes the whole story in one deferred-FK transaction.
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Loop");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        story.SetFollowUp(a.Id, b.Id);
        story.SetFollowUp(b.Id, a.Id);

        await SaveAsync(story, cancellationToken);
        var loaded = await LoadAsync(cancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(b.Id, loaded.FindScene(a.Id)!.FollowUpSceneId);
        Assert.Equal(a.Id, loaded.FindScene(b.Id)!.FollowUpSceneId);
    }

    [Fact]
    public async Task Should_RoundTripEmptyStory_When_NoScenes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("Empty");

        await SaveAsync(story, cancellationToken);
        var loaded = await LoadAsync(cancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal("Empty", loaded.Title);
        Assert.Empty(loaded.Scenes);
        Assert.Null(loaded.StartSceneId);
    }

    [Fact]
    public async Task Should_LeaveStartSceneUnset_When_NoneWasSet()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var story = new Story("No start");
        story.AddScene(SceneKind.Linear, "orphan");

        await SaveAsync(story, cancellationToken);
        var loaded = await LoadAsync(cancellationToken);

        Assert.NotNull(loaded);
        Assert.Null(loaded.StartSceneId);
        Assert.Single(loaded.Scenes);
    }

    [Fact]
    public async Task Should_Throw_When_SavingNullStory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var repository = new SqliteStoryRepository(connection);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveAsync(null!, cancellationToken));
    }

    [Fact]
    public void Should_Throw_When_ConnectionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SqliteStoryRepository(null!));
    }

    [Fact]
    public async Task Should_Throw_When_SaveIsAlreadyCancelled()
    {
        await using var connection = await OpenMigratedAsync(TestContext.Current.CancellationToken);
        var repository = new SqliteStoryRepository(connection);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.SaveAsync(new Story("Story"), cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_LoadIsAlreadyCancelled()
    {
        await using var connection = await OpenMigratedAsync(TestContext.Current.CancellationToken);
        var repository = new SqliteStoryRepository(connection);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.LoadAsync(cancelled.Token));
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

    private static Story BuildRichStory()
    {
        // Every scene kind, both outgoing transition kinds, all three ending outcomes (one labelled,
        // one bare, the mandatory-label 'Other'), an attached image, ordered choices, and a start scene.
        var story = new Story("The Crossroads");
        var start = story.AddScene(SceneKind.Choice, "A fork in the road.");
        start.AttachImage("assets/fork.png");
        var corridor = story.AddScene(SceneKind.Linear, "A long corridor.");
        var death = story.AddScene(SceneKind.Ending, "A grue devours you.", EndingOutcome.Death("Eaten by the grue"));
        var victory = story.AddScene(SceneKind.Ending, "You reach the castle.", EndingOutcome.Victory());
        var other = story.AddScene(SceneKind.Ending, "You wander forever.", EndingOutcome.Other("Lost to the mist"));

        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "Take the dark path", corridor.Id);
        story.WireChoice(start.Id, "Take the bright path", victory.Id);
        story.WireChoice(start.Id, "Stand still", other.Id);
        story.SetFollowUp(corridor.Id, death.Id);

        return story;
    }

    private static void AssertSameStory(Story expected, Story actual)
    {
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.StartSceneId, actual.StartSceneId);
        Assert.Equal(expected.Scenes.Count, actual.Scenes.Count);

        foreach (var expectedScene in expected.Scenes)
        {
            var actualScene = actual.FindScene(expectedScene.Id);
            Assert.NotNull(actualScene);
            Assert.Equal(expectedScene.Kind, actualScene.Kind);
            Assert.Equal(expectedScene.Text, actualScene.Text);
            Assert.Equal(expectedScene.ImagePath, actualScene.ImagePath);
            Assert.Equal(expectedScene.FollowUpSceneId, actualScene.FollowUpSceneId);
            Assert.Equal(expectedScene.Outcome?.Kind, actualScene.Outcome?.Kind);
            Assert.Equal(expectedScene.Outcome?.Label, actualScene.Outcome?.Label);

            Assert.Equal(expectedScene.Choices.Count, actualScene.Choices.Count);
            for (var i = 0; i < expectedScene.Choices.Count; i++)
            {
                Assert.Equal(expectedScene.Choices[i].Label, actualScene.Choices[i].Label);
                Assert.Equal(expectedScene.Choices[i].TargetSceneId, actualScene.Choices[i].TargetSceneId);
            }
        }
    }

    private async Task SaveAsync(Story story, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var repository = new SqliteStoryRepository(connection);
        await repository.SaveAsync(story, cancellationToken);
    }

    private async Task<Story?> LoadAsync(CancellationToken cancellationToken)
    {
        // A fresh connection proves the story survives to disk, not just in the writer's session.
        await using var connection = await OpenMigratedAsync(cancellationToken);
        var repository = new SqliteStoryRepository(connection);
        return await repository.LoadAsync(cancellationToken);
    }

    private async Task<DbConnection> OpenMigratedAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.OpenAsync(_databasePath, cancellationToken);
        await new StoryDatabaseMigrator().MigrateAsync(connection, cancellationToken);
        return connection;
    }
}
