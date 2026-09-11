using System.Globalization;
using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class CanvasModelTests
{
    [Fact]
    public void Should_AddOneEdge_When_ASceneHasAChoice()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.WireChoice(start.Id, "Walk on", target.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        var edge = Assert.Single(model.Edges);
        Assert.Equal(start.Id, edge.Source);
        Assert.Equal(target.Id, edge.Target);
        Assert.Equal("Walk on", edge.Label);
        Assert.Equal(new CanvasEdgeKey(start.Id, 0), edge.Key);
        Assert.False(edge.IsSelfLoop);
    }

    [Fact]
    public void Should_AddAnUnlabelledEdge_When_ASceneHasAFollowUp()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "A corridor");
        var target = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetFollowUp(start.Id, target.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        var edge = Assert.Single(model.Edges);
        Assert.Equal(string.Empty, edge.Label);
        Assert.Equal(new CanvasEdgeKey(start.Id, null), edge.Key);
    }

    [Fact]
    public void Should_SeparateParallelEdges_When_TwoChoicesShareATarget()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.WireChoice(start.Id, "Go left", target.Id);
        story.WireChoice(start.Id, "Go right", target.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        Assert.Equal(2, model.Edges.Count);
        Assert.Equal([0, 1], model.Edges.Select(edge => edge.ParallelIndex));
    }

    [Fact]
    public void Should_MarkASelfLoop_When_AChoiceTargetsItsOwnScene()
    {
        // Arrange
        var story = new Story("Story");
        var loop = story.AddScene(SceneKind.Choice, "A round room");
        story.WireChoice(loop.Id, "Keep walking", loop.Id);
        var positions = PositionEachSceneInOrder(story);

        // Act
        var model = CanvasModel.Build(story, positions);

        // Assert
        var edge = Assert.Single(model.Edges);
        Assert.True(edge.IsSelfLoop);
    }

    [Fact]
    public void Should_HitTestTheTopmostNode_When_NodesOverlap()
    {
        // Scenes.Ordered sorts "A scene" before "B scene"; CanvasModel keeps that order in Nodes,
        // so the topmost (last-painted) node under an overlap is "B scene".

        // Arrange
        var story = new Story("Story");
        var first = story.AddScene(SceneKind.Linear, "A scene");
        var second = story.AddScene(SceneKind.Linear, "B scene");
        var shared = new ScenePosition(100, 100);
        var positions = new Dictionary<SceneId, ScenePosition> { [first.Id] = shared, [second.Id] = shared };

        // Act
        var model = CanvasModel.Build(story, positions);
        var hit = model.HitTest(new ScenePosition(150, 120));

        // Assert
        Assert.Equal(second.Id, hit);
    }

    [Fact]
    public void Should_ReturnNull_When_HitTestingEmptySpace()
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Linear, "Alone");
        var positions = new Dictionary<SceneId, ScenePosition> { [scene.Id] = new ScenePosition(0, 0) };

        // Act
        var model = CanvasModel.Build(story, positions);
        var hit = model.HitTest(new ScenePosition(-500, -500));

        // Assert
        Assert.Null(hit);
    }

    [Fact]
    public void Should_FormatNodeTransformInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-PT");
        try
        {
            var story = new Story("Story");
            var scene = story.AddScene(SceneKind.Linear, "Alone");
            var node = new CanvasNode(scene, new ScenePosition(10.5, 20), IsStart: false);

            // Act
            var transform = node.Transform;

            // Assert
            Assert.Equal("translate(10.5 20)", transform);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static Dictionary<SceneId, ScenePosition> PositionEachSceneInOrder(Story story)
    {
        var positions = new Dictionary<SceneId, ScenePosition>();
        var x = 0.0;
        foreach (var scene in story.Scenes)
        {
            positions[scene.Id] = new ScenePosition(x, 0);
            x += 300;
        }

        return positions;
    }
}
