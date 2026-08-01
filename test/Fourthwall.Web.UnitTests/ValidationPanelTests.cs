using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class ValidationPanelTests : BunitContext
{
    private readonly FakeStoryWorkspace _workspace = new();
    private readonly FakeStoryValidation _validation = new();

    public ValidationPanelTests()
    {
        Services.AddSingleton<IStoryWorkspace>(_workspace);
        Services.AddSingleton<IStoryValidation>(_validation);
    }

    [Fact]
    public async Task Should_ShowNothingValidatedYet_When_FirstRendered()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = Render<ValidationPanel>();

        // Assert — not the same as "validated and clean".
        Assert.NotNull(cut.Find(".validation-idle"));
        Assert.Empty(cut.FindAll(".validation-clean"));
    }

    [Fact]
    public async Task Should_ReportTheStoryClean_When_ValidationFindsNothing()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = Render<ValidationPanel>();

        // Act
        cut.Find("#validate").Click();

        // Assert
        Assert.NotNull(cut.Find(".validation-clean"));
        Assert.Empty(cut.FindAll(".validation-violation"));
    }

    [Fact]
    public async Task Should_ListErrorsAndWarningsApart_When_BothAreFound()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        _validation.Report = new ValidationReport(
        [
            Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, "1 scene cannot be reached.", scene.Id),
            Violation(ValidationRule.EverySceneCanReachEnding, ValidationSeverity.Warning, "1 scene cannot reach an ending.", scene.Id),
        ]);
        var cut = Render<ValidationPanel>();

        // Act
        cut.Find("#validate").Click();

        // Assert
        Assert.Single(cut.FindAll(".validation-error"));
        Assert.Single(cut.FindAll(".validation-warning"));
    }

    [Fact]
    public async Task Should_NameTheRuleAndItsMessage_When_AViolationIsShown()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        _validation.Report = new ValidationReport(
            [Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, "1 scene cannot be reached.", scene.Id)]);
        var cut = Render<ValidationPanel>();

        // Act
        cut.Find("#validate").Click();

        // Assert
        var row = cut.Find(".validation-violation").TextContent;
        Assert.Contains(nameof(ValidationRule.AllScenesReachable), row, StringComparison.Ordinal);
        Assert.Contains("cannot be reached", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_OfferAChipPerOffendingScene_When_AViolationNamesSeveral()
    {
        // Arrange — violations aggregate, so one row often names more than one scene.
        var story = await OpenStoryAsync();
        var first = story.AddScene(SceneKind.Linear, "A storm gathers");
        var second = story.AddScene(SceneKind.Linear, "Below deck");
        _validation.Report = new ValidationReport(
            [Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, "2 scenes cannot be reached.", first.Id, second.Id)]);
        var cut = Render<ValidationPanel>();

        // Act
        cut.Find("#validate").Click();

        // Assert
        Assert.Equal(
            ["A storm gathers", "Below deck"],
            cut.FindAll(".validation-scene").Select(chip => chip.TextContent.Trim()));
    }

    [Fact]
    public async Task Should_SelectTheScene_When_AChipIsChosen()
    {
        // Arrange
        var story = await OpenStoryAsync();
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        _validation.Report = new ValidationReport(
            [Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, "1 scene cannot be reached.", scene.Id)]);
        SceneId? selected = null;
        var cut = Render<ValidationPanel>(parameters => parameters.Add(p => p.OnSceneSelected, id => selected = id));
        cut.Find("#validate").Click();

        // Act
        cut.Find(".validation-scene").Click();

        // Assert
        Assert.Equal(scene.Id, selected);
    }

    [Fact]
    public async Task Should_ShowNoChips_When_AViolationNamesNoScene()
    {
        // Arrange — an orphan asset belongs to no scene; its paths are in the message.
        await OpenStoryAsync();
        _validation.Report = new ValidationReport(
            [Violation(ValidationRule.OrphanAsset, ValidationSeverity.Warning, "1 asset is unreferenced: assets/old.png.")]);
        var cut = Render<ValidationPanel>();

        // Act
        cut.Find("#validate").Click();

        // Assert
        Assert.Single(cut.FindAll(".validation-violation"));
        Assert.Empty(cut.FindAll(".validation-scene"));
    }

    [Fact]
    public async Task Should_DiscardTheReport_When_TheStoryChanges()
    {
        // Arrange — a report describing a story that has since been edited is misleading.
        await OpenStoryAsync();
        var cut = Render<ValidationPanel>();
        cut.Find("#validate").Click();

        // Act
        await _workspace.SaveAsync(TestContext.Current.CancellationToken);

        // Assert
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".validation-idle")));
    }

    [Fact]
    public async Task Should_ReportTheFailure_When_ValidationThrows()
    {
        // Arrange — the asset half reads the file system and can fail like anything else.
        await OpenStoryAsync();
        _validation.FailNext = new IOException("The story folder is gone.");
        var cut = Render<ValidationPanel>();

        // Act
        cut.Find("#validate").Click();

        // Assert
        Assert.Contains("gone", cut.Find(".validation-error-message").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_ShowNothing_When_NoStoryIsOpen()
    {
        // Arrange & Act
        var cut = Render<ValidationPanel>();

        // Assert
        Assert.Empty(cut.FindAll("#validate"));
    }

    private static ValidationViolation Violation(
        ValidationRule rule, ValidationSeverity severity, string message, params SceneId[] sceneIds) =>
        new(rule, severity, message, sceneIds);

    private Task<Story> OpenStoryAsync() =>
        _workspace.CreateAsync(@"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken);
}
