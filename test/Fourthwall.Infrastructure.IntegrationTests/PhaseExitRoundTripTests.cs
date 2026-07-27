using System.Text;
using Fourthwall.Application;
using Fourthwall.Domain;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure.IntegrationTests;

/// <summary>
/// The Phase 2 exit criterion (design doc section 6): a story with images can be created, saved,
/// closed, and reopened from disk with full fidelity, resolvable images, and a correct validation
/// report — proven through the real story package, SQLite repository, asset store, graph, and both
/// validators.
/// </summary>
public sealed class PhaseExitRoundTripTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly StoryValidator _structuralValidator = new(new Graph1xStoryGraphFactory());
    private readonly AssetIntegrityValidator _assetValidator = new();

    public PhaseExitRoundTripTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDirectory);
    }

    [Fact]
    public async Task Should_RoundTripStoryWithImages_When_CreatedSavedClosedAndReopened()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        SceneId startId;
        SceneId victoryId;
        string startImage;
        string victoryImage;

        await using (var package = await StoryPackage.CreateAsync(folder, cancellationToken))
        {
            startImage = await IngestAsync(package, "fork image", cancellationToken);
            victoryImage = await IngestAsync(package, "castle image", cancellationToken);

            var story = new Story("The Crossroads");
            var start = story.AddScene(SceneKind.Choice, "A fork in the road.");
            start.AttachImage(startImage);
            var victory = story.AddScene(SceneKind.Ending, "You reach the castle.", EndingOutcome.Victory());
            victory.AttachImage(victoryImage);
            var death = story.AddScene(SceneKind.Ending, "A grue devours you.", EndingOutcome.Death("Eaten"));
            story.SetStartScene(start.Id);
            story.WireChoice(start.Id, "Take the bright path", victory.Id);
            story.WireChoice(start.Id, "Take the dark path", death.Id);
            startId = start.Id;
            victoryId = victory.Id;

            await package.Repository.SaveAsync(story, cancellationToken);
        }

        await using (var package = await StoryPackage.OpenAsync(folder, cancellationToken))
        {
            var loaded = await package.Repository.LoadAsync(cancellationToken);

            Assert.NotNull(loaded);
            Assert.Equal("The Crossroads", loaded.Title);
            Assert.Equal(3, loaded.Scenes.Count);
            Assert.Equal(startId, loaded.StartSceneId);
            Assert.Equal(startImage, loaded.FindScene(startId)!.ImagePath);
            Assert.Equal(victoryImage, loaded.FindScene(victoryId)!.ImagePath);

            // Both referenced images resolve to real files, and the story is sound on both axes.
            Assert.True(await package.Assets.ExistsAsync(startImage, cancellationToken));
            Assert.True(await package.Assets.ExistsAsync(victoryImage, cancellationToken));
            Assert.True(_structuralValidator.Validate(loaded).IsValid);
            Assert.Empty((await _assetValidator.ValidateAsync(loaded, package.Assets, cancellationToken)).Violations);
        }
    }

    [Fact]
    public async Task Should_ReportBrokenAndOrphanWarnings_When_ReopenedStoryHasAssetMismatches()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        SceneId brokenSceneId;

        await using (var package = await StoryPackage.CreateAsync(folder, cancellationToken))
        {
            // An ingested-but-unreferenced asset (orphan) plus a scene referencing a never-ingested
            // path (broken) — AttachImage allows a not-yet-existing relative path, as D7 anticipates.
            await IngestAsync(package, "unused image", cancellationToken);
            var story = new Story("Mismatched");
            var scene = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
            scene.AttachImage("assets/never-ingested.png");
            story.SetStartScene(scene.Id);
            brokenSceneId = scene.Id;

            await package.Repository.SaveAsync(story, cancellationToken);
        }

        await using (var package = await StoryPackage.OpenAsync(folder, cancellationToken))
        {
            var loaded = await package.Repository.LoadAsync(cancellationToken);
            var report = await _assetValidator.ValidateAsync(loaded!, package.Assets, cancellationToken);

            var broken = Assert.Single(report.Violations, v => v.Rule == ValidationRule.BrokenImageReference);
            Assert.Equal([brokenSceneId], broken.SceneIds);
            Assert.Contains(report.Violations, v => v.Rule == ValidationRule.OrphanAsset);
            Assert.True(report.IsValid);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle during teardown must not fail the test; the temp folder is disposable.
        }
    }

    private static async Task<string> IngestAsync(StoryPackage package, string content, CancellationToken cancellationToken) =>
        await package.Assets.IngestAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)), "png", cancellationToken);

    private string NewStoryFolder() => Path.Combine(_baseDirectory, Guid.NewGuid().ToString("N"));
}
