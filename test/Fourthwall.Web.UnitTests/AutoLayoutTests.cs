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

    private static IStoryGraph CreateGraph(Story story) => new Graph1xStoryGraphFactory().Create(story);
}
