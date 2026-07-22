using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Infrastructure.UnitTests;

/// <remarks>
/// A corpus of hand-built stories run end to end through the <em>real</em> Graph1x adapter and the
/// story validator, each asserting the exact report. This is the Phase-1 exit demonstration — a
/// story built and validated fully in memory — and the standing compensating control for Graph1x's
/// model-generated tests: if the library's traversal ever changed, these known-correct reports break.
/// </remarks>
public class GoldenStoryValidationTests
{
    [Fact]
    public void Should_ReportNothing_When_StoryBranchesCleanlyToTwoEndings()
    {
        // Arrange
        var story = new Story("The Crossroads");
        var start = story.AddScene(SceneKind.Choice, "A fork in the road.");
        var death = story.AddScene(SceneKind.Ending, "A grue devours you.", EndingOutcome.Death());
        var victory = story.AddScene(SceneKind.Ending, "You reach the castle.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "Take the dark path", death.Id);
        story.WireChoice(start.Id, "Take the bright path", victory.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Should_ReportOnlyMissingStart_When_NoStartSceneIsSet()
    {
        // Arrange
        var story = new Story("Story");
        var opening = story.AddScene(SceneKind.Linear, "opening");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetFollowUp(opening.Id, ending.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.Equal([ValidationRule.SingleStartScene], RulesFired(report));
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Should_ReportUnreachableCluster_When_AnOrphanSubgraphExists()
    {
        // Arrange
        var story = BranchingStory();
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        var orphanEnding = story.AddScene(SceneKind.Ending, "Orphan end.", EndingOutcome.Death());
        story.SetFollowUp(orphan.Id, orphanEnding.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.Equal([ValidationRule.AllScenesReachable], RulesFired(report));
        var violation = Single(report, ValidationRule.AllScenesReachable);
        Assert.Equal(ValidationSeverity.Error, violation.Severity);
        Assert.Equal(
            new[] { orphan.Id, orphanEnding.Id }.OrderBy(id => id.Value),
            violation.SceneIds);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Should_ReportDegreeMismatch_When_AChoiceSceneHasASingleChoice()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "The only way", ending.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.Equal([ValidationRule.OutgoingDegreeMatchesKind], RulesFired(report));
        var violation = Single(report, ValidationRule.OutgoingDegreeMatchesKind);
        Assert.Equal([start.Id], violation.SceneIds);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Should_ReportUnreachableAndWalledOffEnding_When_TheStartLoopsForever()
    {
        // A two-scene cycle with an ending nothing points at: the ending is both unreachable from
        // the start (rule 2) and unreachable as an ending (rule 4), and the looping pair can reach
        // no ending (rule 5). Exercises cycle termination in both directions of the real engine.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var second = story.AddScene(SceneKind.Linear, "second");
        var walledOff = story.AddScene(SceneKind.Ending, "Unreachable end.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);
        story.SetFollowUp(start.Id, second.Id);
        story.SetFollowUp(second.Id, start.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.Equal(
            new HashSet<ValidationRule>
            {
                ValidationRule.AllScenesReachable,
                ValidationRule.EndingReachable,
                ValidationRule.EverySceneCanReachEnding,
            },
            RulesFired(report).ToHashSet());
        Assert.Equal([walledOff.Id], Single(report, ValidationRule.AllScenesReachable).SceneIds);
        Assert.Equal(
            new[] { start.Id, second.Id }.OrderBy(id => id.Value),
            Single(report, ValidationRule.EverySceneCanReachEnding).SceneIds);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Should_WarnWithoutInvalidating_When_ATwoSceneDoomLoopIsTrapped()
    {
        // Only rule 5 is violated — every other rule is satisfied, so the story stays valid.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var trapped = story.AddScene(SceneKind.Linear, "trapped");
        var alsoTrapped = story.AddScene(SceneKind.Linear, "also trapped");
        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "Escape", ending.Id);
        story.WireChoice(start.Id, "Wander in", trapped.Id);
        story.SetFollowUp(trapped.Id, alsoTrapped.Id);
        story.SetFollowUp(alsoTrapped.Id, trapped.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.Equal([ValidationRule.EverySceneCanReachEnding], RulesFired(report));
        var violation = Single(report, ValidationRule.EverySceneCanReachEnding);
        Assert.Equal(ValidationSeverity.Warning, violation.Severity);
        Assert.Equal(
            new[] { trapped.Id, alsoTrapped.Id }.OrderBy(id => id.Value),
            violation.SceneIds);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Should_WarnWithoutInvalidating_When_ASelfLoopingSceneIsTrapped()
    {
        // A scene whose only transition is to itself can reach no ending — the transpose walk must
        // terminate on the self-loop and still exclude it from the scenes that reach the ending.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var selfLoop = story.AddScene(SceneKind.Linear, "a scene that loops on itself");
        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "Escape", ending.Id);
        story.WireChoice(start.Id, "Enter the loop", selfLoop.Id);
        story.SetFollowUp(selfLoop.Id, selfLoop.Id);

        // Act
        var report = Validate(story);

        // Assert
        Assert.Equal([ValidationRule.EverySceneCanReachEnding], RulesFired(report));
        var violation = Single(report, ValidationRule.EverySceneCanReachEnding);
        Assert.Equal(ValidationSeverity.Warning, violation.Severity);
        Assert.Equal([selfLoop.Id], violation.SceneIds);
        Assert.True(report.IsValid);
    }

    private static ValidationReport Validate(Story story) =>
        new StoryValidator(new Graph1xStoryGraphFactory()).Validate(story);

    /// <summary>A start scene branching into two endings — satisfies every rule.</summary>
    private static Story BranchingStory()
    {
        var story = new Story("The Crossroads");
        var start = story.AddScene(SceneKind.Choice, "A fork in the road.");
        var death = story.AddScene(SceneKind.Ending, "A grue devours you.", EndingOutcome.Death());
        var victory = story.AddScene(SceneKind.Ending, "You reach the castle.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "Take the dark path", death.Id);
        story.WireChoice(start.Id, "Take the bright path", victory.Id);
        return story;
    }

    private static List<ValidationRule> RulesFired(ValidationReport report) =>
        report.Violations.Select(violation => violation.Rule).ToList();

    private static ValidationViolation Single(ValidationReport report, ValidationRule rule) =>
        Assert.Single(report.Violations, violation => violation.Rule == rule);
}
