using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

/// <remarks>
/// The in-memory graph is the test double standing in for the Graph1x adapter (M4). It is
/// tested in its own right so that a bug in the double cannot silently mask a validator bug.
/// </remarks>
public class InMemoryStoryGraphTests
{
    [Fact]
    public void Should_IncludeOrigin_When_SceneHasNoOutgoingTransitions()
    {
        // Reachability is reflexive: the start scene counts as reachable from itself.

        // Arrange
        var story = new Story("Story");
        var only = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var graph = new InMemoryStoryGraph(story);

        // Act
        var reachable = graph.ReachableFrom(only.Id);

        // Assert
        Assert.Equal([only.Id], reachable);
    }

    [Fact]
    public void Should_FollowChoicesAndFollowUps_When_WalkingForward()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var corridor = story.AddScene(SceneKind.Linear, "A corridor");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var other = story.AddScene(SceneKind.Ending, "Another end.", EndingOutcome.Death());
        story.WireChoice(start.Id, "Walk on", corridor.Id);
        story.WireChoice(start.Id, "Stop here", other.Id);
        story.SetFollowUp(corridor.Id, ending.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var reachable = graph.ReachableFrom(start.Id);

        // Assert
        Assert.Equal(
            new HashSet<SceneId> { start.Id, corridor.Id, ending.Id, other.Id },
            reachable);
    }

    [Fact]
    public void Should_ExcludeUnreachableScenes_When_WalkingForward()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        story.SetFollowUp(start.Id, ending.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var reachable = graph.ReachableFrom(start.Id);

        // Assert
        Assert.DoesNotContain(orphan.Id, reachable);
    }

    [Fact]
    public void Should_Terminate_When_StoryContainsCycles()
    {
        // Cycles are legal in a gamebook, so traversal must not loop forever.

        // Arrange
        var story = new Story("Story");
        var first = story.AddScene(SceneKind.Linear, "first");
        var second = story.AddScene(SceneKind.Linear, "second");
        story.SetFollowUp(first.Id, second.Id);
        story.SetFollowUp(second.Id, first.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var reachable = graph.ReachableFrom(first.Id);

        // Assert
        Assert.Equal(new HashSet<SceneId> { first.Id, second.Id }, reachable);
    }

    [Fact]
    public void Should_IncludeTargets_When_WalkingBackward()
    {
        // Reverse reachability is reflexive too: an ending trivially reaches an ending.

        // Arrange
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var graph = new InMemoryStoryGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId> { ending.Id });

        // Assert
        Assert.Equal([ending.Id], canReach);
    }

    [Fact]
    public void Should_ReturnScenesReachingTargetTransitively_When_WalkingBackward()
    {
        // Arrange
        var story = new Story("Story");
        var first = story.AddScene(SceneKind.Linear, "first");
        var second = story.AddScene(SceneKind.Linear, "second");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetFollowUp(first.Id, second.Id);
        story.SetFollowUp(second.Id, ending.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId> { ending.Id });

        // Assert
        Assert.Equal(
            new HashSet<SceneId> { first.Id, second.Id, ending.Id },
            canReach);
    }

    [Fact]
    public void Should_ExcludeScenesInDoomLoop_When_WalkingBackward()
    {
        // Arrange
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var trapped = story.AddScene(SceneKind.Linear, "trapped");
        var alsoTrapped = story.AddScene(SceneKind.Linear, "also trapped");
        story.SetFollowUp(trapped.Id, alsoTrapped.Id);
        story.SetFollowUp(alsoTrapped.Id, trapped.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId> { ending.Id });

        // Assert
        Assert.DoesNotContain(trapped.Id, canReach);
        Assert.DoesNotContain(alsoTrapped.Id, canReach);
    }

    [Fact]
    public void Should_ReturnEmpty_When_NoTargetsGiven()
    {
        // Arrange
        var story = new Story("Story");
        _ = story.AddScene(SceneKind.Linear, "scene");
        var graph = new InMemoryStoryGraph(story);

        // Act
        var canReach = graph.ScenesThatCanReachAny(new HashSet<SceneId>());

        // Assert
        Assert.Empty(canReach);
    }

    [Fact]
    public void Should_MapOriginToZero_When_DepthIsQueried()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var graph = new InMemoryStoryGraph(story);

        // Act
        var depths = graph.DepthFrom(start.Id);

        // Assert
        Assert.Equal(0, depths[start.Id]);
    }

    [Fact]
    public void Should_UseTheShorterPath_When_ASceneIsReachableTwoWays()
    {
        // A diamond: one route is a single hop, the other goes through an extra scene. Both scenes
        // are asserted so a depth flattened to "1 if reachable" (which would also satisfy a
        // target-only assertion, since target sits at depth 1 either way) cannot pass by accident.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "start");
        var detour = story.AddScene(SceneKind.Linear, "detour");
        var target = story.AddScene(SceneKind.Ending, "target", EndingOutcome.Victory());
        story.WireChoice(start.Id, "the long way", detour.Id);
        story.SetFollowUp(detour.Id, target.Id);
        story.WireChoice(start.Id, "the short way", target.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var depths = graph.DepthFrom(start.Id);

        // Assert
        Assert.Equal(1, depths[target.Id]);
        Assert.Equal(1, depths[detour.Id]);
    }

    [Fact]
    public void Should_AccumulateDepthAcrossMultipleHops_When_WalkingAChain()
    {
        // A single hop is not enough to distinguish "depth" from "is reachable"; a chain long
        // enough to reach depth two closes that gap.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var middle = story.AddScene(SceneKind.Linear, "middle");
        var end = story.AddScene(SceneKind.Linear, "end");
        story.SetFollowUp(start.Id, middle.Id);
        story.SetFollowUp(middle.Id, end.Id);
        var graph = new InMemoryStoryGraph(story);

        // Act
        var depths = graph.DepthFrom(start.Id);

        // Assert
        Assert.Equal(1, depths[middle.Id]);
        Assert.Equal(2, depths[end.Id]);
    }

    [Fact]
    public void Should_OmitUnreachableScenes_When_DepthIsQueried()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        var graph = new InMemoryStoryGraph(story);

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
        var graph = new InMemoryStoryGraph(story);

        // Act
        var depths = graph.DepthFrom(a.Id);

        // Assert
        Assert.Equal(0, depths[a.Id]);
        Assert.Equal(1, depths[b.Id]);
    }
}
