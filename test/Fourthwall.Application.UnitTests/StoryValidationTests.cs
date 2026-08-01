using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

public class StoryValidationTests
{
    [Fact]
    public void Should_ThrowArgumentNullException_When_StructuralValidatorIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new StoryValidation(null!, new AssetIntegrityValidator()));
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_AssetValidatorIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StoryValidation(CreateStructural(), null!));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_StoryIsNull()
    {
        // Arrange
        var validation = CreateValidation();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => validation.ValidateAsync(null!, new InMemoryAssetStore(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_AssetStoreIsNull()
    {
        // Arrange
        var validation = CreateValidation();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => validation.ValidateAsync(ValidStory(), null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReportNothing_When_TheStoryIsValidAndItsAssetsAgree()
    {
        // Arrange
        var validation = CreateValidation();

        // Act
        var report = await validation.ValidateAsync(
            ValidStory(), new InMemoryAssetStore(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public async Task Should_ReportStructuralViolations_When_TheStoryIsBroken()
    {
        // Arrange — no start scene is rule 1.
        var story = new Story("Story");
        story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        var validation = CreateValidation();

        // Act
        var report = await validation.ValidateAsync(
            story, new InMemoryAssetStore(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(report.IsValid);
        Assert.Contains(report.Violations, violation => violation.Rule == ValidationRule.SingleStartScene);
    }

    [Fact]
    public async Task Should_ReportAssetViolations_When_AnImageIsMissing()
    {
        // Arrange — rule 6a, which the structural validator knows nothing about.
        var story = ValidStory();
        story.Scenes.First().AttachImage("assets/gone.png");
        var validation = CreateValidation();

        // Act
        var report = await validation.ValidateAsync(
            story, new InMemoryAssetStore(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(report.Violations, violation => violation.Rule == ValidationRule.BrokenImageReference);
    }

    [Fact]
    public async Task Should_ReportBothHalves_When_StructureAndAssetsAreBothBroken()
    {
        // Arrange — the whole point of composing: one report, not two.
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        ending.AttachImage("assets/gone.png");
        var validation = CreateValidation();

        // Act
        var report = await validation.ValidateAsync(
            story, new InMemoryAssetStore(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(report.Violations, violation => violation.Rule == ValidationRule.SingleStartScene);
        Assert.Contains(report.Violations, violation => violation.Rule == ValidationRule.BrokenImageReference);
        Assert.False(report.IsValid);
    }

    [Fact]
    public async Task Should_OrderStructuralViolationsFirst_When_BothHalvesReport()
    {
        // Arrange — rules read in the order the design states them: 1-5, then 6.
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        ending.AttachImage("assets/gone.png");
        var validation = CreateValidation();

        // Act
        var report = await validation.ValidateAsync(
            story, new InMemoryAssetStore(), TestContext.Current.CancellationToken);

        // Assert
        var assetRules = new[] { ValidationRule.BrokenImageReference, ValidationRule.OrphanAsset };
        var firstAsset = report.Violations.ToList().FindIndex(violation => assetRules.Contains(violation.Rule));
        var lastStructural = report.Violations.ToList().FindLastIndex(violation => !assetRules.Contains(violation.Rule));
        Assert.True(lastStructural < firstAsset);
    }

    [Fact]
    public async Task Should_KeepWarningsSeparateFromErrors_When_BothArePresent()
    {
        // Arrange
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        ending.AttachImage("assets/gone.png");
        var validation = CreateValidation();

        // Act
        var report = await validation.ValidateAsync(
            story, new InMemoryAssetStore(), TestContext.Current.CancellationToken);

        // Assert
        Assert.All(report.Errors, violation => Assert.Equal(ValidationSeverity.Error, violation.Severity));
        Assert.Contains(report.Warnings, violation => violation.Rule == ValidationRule.BrokenImageReference);
    }

    [Fact]
    public async Task Should_Throw_When_AlreadyCancelled()
    {
        // Arrange
        var validation = CreateValidation();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => validation.ValidateAsync(ValidStory(), new InMemoryAssetStore(), cancelled.Token));
    }

    private static Story ValidStory()
    {
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        story.SetStartScene(ending.Id);
        return story;
    }

    private static StoryValidator CreateStructural() => new(new InMemoryStoryGraphFactory());

    private static StoryValidation CreateValidation() => new(CreateStructural(), new AssetIntegrityValidator());
}
