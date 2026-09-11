using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Infrastructure;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class AutoLayoutTests
{
    private static readonly IReadOnlyDictionary<SceneId, ScenePosition> NoSavedPositions =
        new Dictionary<SceneId, ScenePosition>();

    [Fact]
    public void Should_PlaceTheStartSceneInTheFirstColumn_When_LayoutIsComputed()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        story.SetStartScene(start.Id);
        var graph = CreateGraph(story);

        // Act
        var positions = AutoLayout.Place(story, graph, NoSavedPositions);

        // Assert
        Assert.Equal(40, positions[start.Id].X);
    }

    [Fact]
    public void Should_PlaceAnUnreachableSceneInATrailingColumn_When_LayoutIsComputed()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        story.SetStartScene(start.Id);
        var graph = CreateGraph(story);

        // Act
        var positions = AutoLayout.Place(story, graph, NoSavedPositions);

        // Assert
        Assert.True(positions[orphan.Id].X > positions[start.Id].X);
    }

    [Fact]
    public void Should_PlaceEveryScene_When_TheStoryHasNoStartScene()
    {
        // Arrange
        var story = new Story("Story");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        var graph = CreateGraph(story);

        // Act
        var positions = AutoLayout.Place(story, graph, NoSavedPositions);

        // Assert
        Assert.True(positions.ContainsKey(a.Id));
        Assert.True(positions.ContainsKey(b.Id));
    }

    [Fact]
    public void Should_KeepASavedPosition_When_OneExists()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        story.SetStartScene(start.Id);
        var graph = CreateGraph(story);
        var saved = new Dictionary<SceneId, ScenePosition> { [start.Id] = new ScenePosition(999, 999) };

        // Act
        var positions = AutoLayout.Place(story, graph, saved);

        // Assert
        Assert.Equal(new ScenePosition(999, 999), positions[start.Id]);
    }

    [Fact]
    public void Should_OrderRowsDeterministically_When_ScenesShareAColumn()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "start");
        var first = story.AddScene(SceneKind.Linear, "A branch");
        var second = story.AddScene(SceneKind.Linear, "B branch");
        story.WireChoice(start.Id, "Go left", first.Id);
        story.WireChoice(start.Id, "Go right", second.Id);
        story.SetStartScene(start.Id);
        var graph = CreateGraph(story);

        // Act
        var positions = AutoLayout.Place(story, graph, NoSavedPositions);

        // Assert
        Assert.Equal(positions[first.Id].X, positions[second.Id].X);
        Assert.True(positions[first.Id].Y < positions[second.Id].Y);
    }

    [Fact]
    public void Should_NotReserveARowForASavedPosition_When_AnAutoPlacedSceneSharesItsColumn()
    {
        // AutoLayout does not try to dodge a saved position when placing an unsaved scene into the
        // same column — the milestone plan accepts the overlap ("drag resolves them") rather than
        // reverse-engineering a row index from an arbitrary saved coordinate. This test pins that
        // as the deliberate choice it is, not an accident: with no start scene, both scenes fall
        // into the same trailing column, and the auto-placed one lands exactly on the saved one.

        // Arrange
        var story = new Story("Story");
        var savedScene = story.AddScene(SceneKind.Linear, "saved");
        var placedScene = story.AddScene(SceneKind.Linear, "placed");
        var graph = CreateGraph(story);
        var saved = new Dictionary<SceneId, ScenePosition> { [savedScene.Id] = new ScenePosition(40, 40) };

        // Act
        var positions = AutoLayout.Place(story, graph, saved);

        // Assert
        Assert.Equal(positions[savedScene.Id], positions[placedScene.Id]);
    }

    [Fact]
    public void Should_ReturnEmpty_When_TheStoryHasNoScenes()
    {
        // Arrange
        var story = new Story("Story");
        var graph = CreateGraph(story);

        // Act
        var positions = AutoLayout.Place(story, graph, NoSavedPositions);

        // Assert
        Assert.Empty(positions);
    }

    [Fact]
    public void Should_Throw_When_StoryIsNull()
    {
        // Arrange
        var graph = CreateGraph(new Story("Story"));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AutoLayout.Place(null!, graph, NoSavedPositions));
    }

    [Fact]
    public void Should_Throw_When_GraphIsNull()
    {
        // Arrange
        var story = new Story("Story");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AutoLayout.Place(story, null!, NoSavedPositions));
    }

    [Fact]
    public void Should_Throw_When_SavedPositionsIsNull()
    {
        // Arrange
        var story = new Story("Story");
        var graph = CreateGraph(story);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AutoLayout.Place(story, graph, null!));
    }

    private static IStoryGraph CreateGraph(Story story) => new Graph1xStoryGraphFactory().Create(story);
}
