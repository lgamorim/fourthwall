using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Infrastructure.UnitTests;

public class Graph1xStoryGraphTests
{
    [Fact]
    public void Should_IncludeOrigin_When_ForwardReachabilityIsQueried()
    {
        // Reachability is reflexive: the start scene counts as reachable from the start.
        // Rules 2 and 4 rely on this, so it gets its own test rather than being incidental.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var next = story.AddScene(SceneKind.Linear, "next");
        story.SetFollowUp(start.Id, next.Id);
        var graph = CreateGraph(story);

        // Act
        var reachable = graph.ReachableFrom(start.Id);

        // Assert
        Assert.Contains(start.Id, reachable);
    }

    [Fact]
    public void Should_ReachEveryForwardScene_When_WalkingALinearChain()
    {
        // Arrange
        var story = new Story("Story");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        var c = story.AddScene(SceneKind.Ending, "c", EndingOutcome.Victory());
        story.SetFollowUp(a.Id, b.Id);
        story.SetFollowUp(b.Id, c.Id);
        var graph = CreateGraph(story);

        // Act & Assert
        Assert.Equal(new HashSet<SceneId> { a.Id, b.Id, c.Id }, graph.ReachableFrom(a.Id).ToHashSet());
        Assert.Equal(new HashSet<SceneId> { c.Id }, graph.ReachableFrom(c.Id).ToHashSet());
    }

    [Fact]
    public void Should_TreatIsolatedSceneAsReachableOnlyFromItself_When_ItHasNoEdges()
    {
        // An orphan scene with no wiring must still be a vertex, or the reachability rules would
        // silently drop it (Graph1x throws if asked to walk from an absent vertex).

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var reachableEnding = story.AddScene(SceneKind.Ending, "end", EndingOutcome.Victory());
        story.SetFollowUp(start.Id, reachableEnding.Id);
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        var graph = CreateGraph(story);

        // Act & Assert
        Assert.DoesNotContain(orphan.Id, graph.ReachableFrom(start.Id));
        Assert.Equal(new HashSet<SceneId> { orphan.Id }, graph.ReachableFrom(orphan.Id).ToHashSet());
    }

    [Fact]
    public void Should_Terminate_When_StoryContainsASelfLoop()
    {
        // Arrange
        var story = new Story("Story");
        var loop = story.AddScene(SceneKind.Linear, "loop");
        story.SetFollowUp(loop.Id, loop.Id);
        var graph = CreateGraph(story);

        // Act & Assert
        Assert.Equal(new HashSet<SceneId> { loop.Id }, graph.ReachableFrom(loop.Id).ToHashSet());
    }

    [Fact]
    public void Should_Terminate_When_StoryContainsATwoSceneCycle()
    {
        // Arrange
        var story = new Story("Story");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        story.SetFollowUp(a.Id, b.Id);
        story.SetFollowUp(b.Id, a.Id);
        var graph = CreateGraph(story);

        // Act & Assert
        Assert.Equal(new HashSet<SceneId> { a.Id, b.Id }, graph.ReachableFrom(a.Id).ToHashSet());
    }

    [Fact]
    public void Should_IncludeTargets_When_ReverseReachabilityIsQueried()
    {
        // The mirror of the forward reflexivity contract: an ending trivially reaches an ending.
        // Rule 5 relies on it, so it too gets its own test.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var ending = story.AddScene(SceneKind.Ending, "end", EndingOutcome.Victory());
        story.SetFollowUp(start.Id, ending.Id);
        var graph = CreateGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId> { ending.Id });

        // Assert
        Assert.Contains(ending.Id, canReach);
    }

    [Fact]
    public void Should_ReturnEmpty_When_ReverseReachabilityHasNoTargets()
    {
        // Arrange
        var story = new Story("Story");
        story.AddScene(SceneKind.Linear, "lonely");
        var graph = CreateGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId>());

        // Assert
        Assert.Empty(canReach);
    }

    [Fact]
    public void Should_FindEverySceneLeadingToAnEnding_When_WalkingBackward()
    {
        // Arrange
        var story = new Story("Story");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        var ending = story.AddScene(SceneKind.Ending, "end", EndingOutcome.Victory());
        var trapped = story.AddScene(SceneKind.Linear, "trapped");
        story.SetFollowUp(a.Id, b.Id);
        story.SetFollowUp(b.Id, ending.Id);
        story.SetFollowUp(trapped.Id, trapped.Id);
        var graph = CreateGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId> { ending.Id });

        // Assert
        Assert.Equal(new HashSet<SceneId> { a.Id, b.Id, ending.Id }, canReach.ToHashSet());
        Assert.DoesNotContain(trapped.Id, canReach);
    }

    [Fact]
    public void Should_MapOriginToZero_When_DepthIsQueried()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var graph = CreateGraph(story);

        // Act
        var depths = graph.DepthFrom(start.Id);

        // Assert
        Assert.Equal(0, depths[start.Id]);
    }

    [Fact]
    public void Should_UseTheShorterPath_When_ASceneIsReachableTwoWays()
    {
        // A diamond: one route is a single hop, the other goes through an extra scene. Depth must
        // report the shorter one, matching Dijkstra semantics rather than any particular traversal
        // order.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "start");
        var detour = story.AddScene(SceneKind.Linear, "detour");
        var target = story.AddScene(SceneKind.Ending, "target", EndingOutcome.Victory());
        story.WireChoice(start.Id, "the long way", detour.Id);
        story.SetFollowUp(detour.Id, target.Id);
        story.WireChoice(start.Id, "the short way", target.Id);
        var graph = CreateGraph(story);

        // Act
        var depths = graph.DepthFrom(start.Id);

        // Assert
        Assert.Equal(1, depths[target.Id]);
    }

    [Fact]
    public void Should_OmitUnreachableScenes_When_DepthIsQueried()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        var graph = CreateGraph(story);

        // Act
        var depths = graph.DepthFrom(start.Id);

        // Assert
        Assert.False(depths.ContainsKey(orphan.Id));
    }

    [Fact]
    public void Should_Terminate_When_DepthIsQueriedOnACycle()
    {
        // Arrange
        var story = new Story("Story");
        var a = story.AddScene(SceneKind.Linear, "a");
        var b = story.AddScene(SceneKind.Linear, "b");
        story.SetFollowUp(a.Id, b.Id);
        story.SetFollowUp(b.Id, a.Id);
        var graph = CreateGraph(story);

        // Act
        var depths = graph.DepthFrom(a.Id);

        // Assert
        Assert.Equal(0, depths[a.Id]);
        Assert.Equal(1, depths[b.Id]);
    }

    private static IStoryGraph CreateGraph(Story story) => new Graph1xStoryGraphFactory().Create(story);
}
