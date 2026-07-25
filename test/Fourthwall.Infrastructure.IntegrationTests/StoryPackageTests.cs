using System.Text;
using Fourthwall.Domain;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class StoryPackageTests : IDisposable
{
    private readonly string _baseDirectory;

    public StoryPackageTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDirectory);
    }

    [Fact]
    public async Task Should_CreateMigratedFolder_When_Created()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();

        await using var package = await StoryPackage.CreateAsync(folder, cancellationToken);

        Assert.True(File.Exists(Path.Combine(folder, "story.db")));
        Assert.True(Directory.Exists(Path.Combine(folder, "assets")));
        Assert.Null(await package.Repository.LoadAsync(cancellationToken));
        Assert.Empty(await package.Assets.ListAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_RoundTripStoryAndImage_When_ReopenedFromDisk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        SceneId sceneId;
        string imagePath;

        await using (var package = await StoryPackage.CreateAsync(folder, cancellationToken))
        {
            imagePath = await package.Assets.IngestAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("scene image")), "png", cancellationToken);
            var story = new Story("Illustrated");
            var scene = story.AddScene(SceneKind.Ending, "The end.", EndingOutcome.Victory());
            scene.AttachImage(imagePath);
            story.SetStartScene(scene.Id);
            sceneId = scene.Id;
            await package.Repository.SaveAsync(story, cancellationToken);
        }

        await using (var package = await StoryPackage.OpenAsync(folder, cancellationToken))
        {
            var loaded = await package.Repository.LoadAsync(cancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(imagePath, loaded.FindScene(sceneId)!.ImagePath);
            Assert.True(await package.Assets.ExistsAsync(imagePath, cancellationToken));
        }
    }

    [Fact]
    public async Task Should_AllowReopen_When_Disposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();

        await using (await StoryPackage.CreateAsync(folder, cancellationToken))
        {
        }

        await using var reopened = await StoryPackage.OpenAsync(folder, cancellationToken);
        Assert.Null(await reopened.Repository.LoadAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_CreatingWhereStoryAlreadyExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        await using (await StoryPackage.CreateAsync(folder, cancellationToken))
        {
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => StoryPackage.CreateAsync(folder, cancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_OpeningWhereNoStoryExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => StoryPackage.OpenAsync(NewStoryFolder(), cancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_CreatePathIsBlank(string folder)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => StoryPackage.CreateAsync(folder, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_OpenPathIsBlank(string folder)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => StoryPackage.OpenAsync(folder, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_CreateIsAlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => StoryPackage.CreateAsync(NewStoryFolder(), cancelled.Token));
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

    private string NewStoryFolder() => Path.Combine(_baseDirectory, Guid.NewGuid().ToString("N"));
}
