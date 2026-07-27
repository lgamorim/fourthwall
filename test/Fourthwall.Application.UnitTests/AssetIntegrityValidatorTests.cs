using System.Text;
using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

public class AssetIntegrityValidatorTests
{
    [Fact]
    public async Task Should_ThrowArgumentNullException_When_StoryIsNull()
    {
        var validator = new AssetIntegrityValidator();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => validator.ValidateAsync(null!, new InMemoryAssetStore(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_AssetStoreIsNull()
    {
        var validator = new AssetIntegrityValidator();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => validator.ValidateAsync(new Story("Story"), null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReportNothing_When_EveryImageResolvesAndNoOrphans()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryAssetStore();
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        scene.AttachImage(await IngestAsync(store, "picture", cancellationToken));

        var report = await new AssetIntegrityValidator().ValidateAsync(story, store, cancellationToken);

        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public async Task Should_ReportBrokenImageReference_When_SceneImageDoesNotResolve()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryAssetStore();
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        scene.AttachImage("assets/never-ingested.png");

        var report = await new AssetIntegrityValidator().ValidateAsync(story, store, cancellationToken);

        var violation = Assert.Single(report.Violations);
        Assert.Equal(ValidationRule.BrokenImageReference, violation.Rule);
        Assert.Equal(ValidationSeverity.Warning, violation.Severity);
        Assert.Equal([scene.Id], violation.SceneIds);
        Assert.True(report.IsValid);
    }

    [Fact]
    public async Task Should_ListEveryBrokenScene_When_MultipleImagesMissing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryAssetStore();
        var story = new Story("Story");
        var first = story.AddScene(SceneKind.Linear, "one");
        first.AttachImage("assets/missing-one.png");
        var second = story.AddScene(SceneKind.Linear, "two");
        second.AttachImage("assets/missing-two.png");

        var report = await new AssetIntegrityValidator().ValidateAsync(story, store, cancellationToken);

        var violation = Assert.Single(report.Violations, v => v.Rule == ValidationRule.BrokenImageReference);
        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id.Value),
            violation.SceneIds.OrderBy(id => id.Value));
    }

    [Fact]
    public async Task Should_ReportOrphanAsset_When_AssetIsUnreferenced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryAssetStore();
        var orphan = await IngestAsync(store, "unused", cancellationToken);

        var report = await new AssetIntegrityValidator().ValidateAsync(new Story("Story"), store, cancellationToken);

        var violation = Assert.Single(report.Violations);
        Assert.Equal(ValidationRule.OrphanAsset, violation.Rule);
        Assert.Equal(ValidationSeverity.Warning, violation.Severity);
        Assert.Empty(violation.SceneIds);
        Assert.Contains(orphan, violation.Message);
        Assert.True(report.IsValid);
    }

    [Fact]
    public async Task Should_ReportBoth_When_BrokenReferenceAndOrphanCoexist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryAssetStore();
        await IngestAsync(store, "unused", cancellationToken);
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
        scene.AttachImage("assets/never-ingested.png");

        var report = await new AssetIntegrityValidator().ValidateAsync(story, store, cancellationToken);

        Assert.Contains(report.Violations, v => v.Rule == ValidationRule.BrokenImageReference);
        Assert.Contains(report.Violations, v => v.Rule == ValidationRule.OrphanAsset);
        Assert.All(report.Violations, v => Assert.Equal(ValidationSeverity.Warning, v.Severity));
    }

    [Fact]
    public async Task Should_IgnoreScenesWithoutImage_When_Validated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryAssetStore();
        var story = new Story("Story");
        story.AddScene(SceneKind.Ending, "No image here.", EndingOutcome.Death());

        var report = await new AssetIntegrityValidator().ValidateAsync(story, store, cancellationToken);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public async Task Should_ReportNothing_When_StoryAndStoreAreEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var report = await new AssetIntegrityValidator()
            .ValidateAsync(new Story("Story"), new InMemoryAssetStore(), cancellationToken);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public async Task Should_Throw_When_AlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AssetIntegrityValidator().ValidateAsync(new Story("Story"), new InMemoryAssetStore(), cancelled.Token));
    }

    private static async Task<string> IngestAsync(InMemoryAssetStore store, string content, CancellationToken cancellationToken) =>
        await store.IngestAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)), "png", cancellationToken);
}
