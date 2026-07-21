using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

public class StoryValidatorTests
{
    [Fact]
    public void Should_ThrowArgumentNullException_When_GraphFactoryIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StoryValidator(null!));
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_StoryIsNull()
    {
        // Arrange
        var validator = CreateValidator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void Should_ReportNothing_When_StoryIsValid()
    {
        // Arrange
        var story = ValidStory();
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Should_ReportMissingStart_When_NoStartSceneIsSet()
    {
        // Arrange
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var opening = story.AddScene(SceneKind.Linear, "opening");
        story.SetFollowUp(opening.Id, ending.Id);
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        var violation = Single(report, ValidationRule.SingleStartScene);
        Assert.Equal(ValidationSeverity.Error, violation.Severity);
        Assert.Empty(violation.SceneIds);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Should_ReportOnlyMissingStart_When_StoryIsEmpty()
    {
        // Arrange
        var story = new Story("Story");
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        Assert.Single(report.Violations);
        Assert.Equal(ValidationRule.SingleStartScene, report.Violations[0].Rule);
    }

    [Fact]
    public void Should_SkipReachabilityRules_When_NoStartSceneIsSet()
    {
        // Rules 2 and 4 depend on a start scene; rule 1 already reported its absence.

        // Arrange
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var opening = story.AddScene(SceneKind.Linear, "opening");
        story.SetFollowUp(opening.Id, ending.Id);
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        AssertNoViolation(report, ValidationRule.AllScenesReachable);
        AssertNoViolation(report, ValidationRule.EndingReachable);
    }

    [Fact]
    public void Should_ReportUnreachableScenes_When_OrphanClusterExists()
    {
        // Arrange
        var story = ValidStory();
        var orphan = story.AddScene(SceneKind.Linear, "orphan");
        var orphanEnding = story.AddScene(SceneKind.Ending, "Orphan end.", EndingOutcome.Death());
        story.SetFollowUp(orphan.Id, orphanEnding.Id);
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        var violation = Single(report, ValidationRule.AllScenesReachable);
        Assert.Equal(ValidationSeverity.Error, violation.Severity);
        Assert.Equal(
            new HashSet<SceneId> { orphan.Id, orphanEnding.Id },
            violation.SceneIds.ToHashSet());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Should_ReportDegreeMismatch_When_ChoiceSceneHasFewerThanTwoChoices(int choiceCount)
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Choice, "A fork");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);

        for (var i = 0; i < choiceCount; i++)
        {
            story.WireChoice(start.Id, $"Choice {i}", ending.Id);
        }

        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        var violation = Single(report, ValidationRule.OutgoingDegreeMatchesKind);
        Assert.Equal(ValidationSeverity.Error, violation.Severity);
        Assert.Contains(start.Id, violation.SceneIds);
    }

    [Fact]
    public void Should_ReportDegreeMismatch_When_LinearSceneHasNoFollowUp()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "a corridor going nowhere");
        story.SetStartScene(start.Id);
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        var violation = Single(report, ValidationRule.OutgoingDegreeMatchesKind);
        Assert.Contains(start.Id, violation.SceneIds);
    }

    [Fact]
    public void Should_NotReportDegreeMismatch_When_EverySceneIsCorrectlyWired()
    {
        // Arrange
        var story = ValidStory();
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        AssertNoViolation(report, ValidationRule.OutgoingDegreeMatchesKind);
    }

    [Fact]
    public void Should_ReportNoReachableEnding_When_OnlyEndingIsUnreachable()
    {
        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var second = story.AddScene(SceneKind.Linear, "second");
        var strandedEnding = story.AddScene(SceneKind.Ending, "Unreachable end.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);
        story.SetFollowUp(start.Id, second.Id);
        story.SetFollowUp(second.Id, start.Id);
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        var violation = Single(report, ValidationRule.EndingReachable);
        Assert.Equal(ValidationSeverity.Error, violation.Severity);
        Assert.False(report.IsValid);
        Assert.NotNull(story.FindScene(strandedEnding.Id));
    }

    [Fact]
    public void Should_SkipReachEndingRule_When_StoryHasNoEndingsAtAll()
    {
        // Rule 4 already reports the absence of endings; flagging every scene under rule 5
        // as well would be pure noise.

        // Arrange
        var story = new Story("Story");
        var start = story.AddScene(SceneKind.Linear, "start");
        var second = story.AddScene(SceneKind.Linear, "second");
        story.SetStartScene(start.Id);
        story.SetFollowUp(start.Id, second.Id);
        story.SetFollowUp(second.Id, start.Id);
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        AssertNoViolation(report, ValidationRule.EverySceneCanReachEnding);
        Assert.Contains(report.Violations, violation => violation.Rule == ValidationRule.EndingReachable);
    }

    [Fact]
    public void Should_WarnWithoutInvalidating_When_ScenesAreCaughtInADoomLoop()
    {
        // Arrange: only rule 5 is violated — every other rule is satisfied.
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
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        var violation = Single(report, ValidationRule.EverySceneCanReachEnding);
        Assert.Equal(ValidationSeverity.Warning, violation.Severity);
        Assert.Equal(
            new HashSet<SceneId> { trapped.Id, alsoTrapped.Id },
            violation.SceneIds.ToHashSet());
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Should_NotFlagEndings_When_CheckingThatScenesCanReachAnEnding()
    {
        // Arrange
        var story = ValidStory();
        var validator = CreateValidator();

        // Act
        var report = validator.Validate(story);

        // Assert
        AssertNoViolation(report, ValidationRule.EverySceneCanReachEnding);
    }

    [Fact]
    public void Should_ReportEachBrokenRuleInTurn_When_ValidStoryIsDegradedOneStepAtATime()
    {
        // End-to-end sanity across the whole rule set.
        var validator = CreateValidator();

        // A complete story is clean.
        var story = ValidStory();
        Assert.True(validator.Validate(story).IsValid);

        // Adding an orphan cluster breaks reachability only.
        var orphan = story.AddScene(SceneKind.Ending, "Orphan end.", EndingOutcome.Death());
        var afterOrphan = validator.Validate(story);
        Assert.Contains(afterOrphan.Violations, v => v.Rule == ValidationRule.AllScenesReachable);
        Assert.Contains(orphan.Id, Single(afterOrphan, ValidationRule.AllScenesReachable).SceneIds);

        // Removing the start scene additionally breaks rule 1 and silences the graph rules.
        var start = story.FindScene(story.StartSceneId!.Value)!;
        story.RemoveScene(start.Id);
        var afterRemoval = validator.Validate(story);
        Assert.Contains(afterRemoval.Violations, v => v.Rule == ValidationRule.SingleStartScene);
        AssertNoViolation(afterRemoval, ValidationRule.AllScenesReachable);
        AssertNoViolation(afterRemoval, ValidationRule.EndingReachable);
        Assert.False(afterRemoval.IsValid);
    }

    private static StoryValidator CreateValidator() => new(new InMemoryStoryGraphFactory());

    /// <summary>A start scene branching into two endings — satisfies every rule.</summary>
    private static Story ValidStory()
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

    private static ValidationViolation Single(ValidationReport report, ValidationRule rule) =>
        Assert.Single(report.Violations, violation => violation.Rule == rule);

    private static void AssertNoViolation(ValidationReport report, ValidationRule rule) =>
        Assert.DoesNotContain(report.Violations, violation => violation.Rule == rule);
}
