using System.Text.Json;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class JsonRecentStoriesStoreTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly string _baseDirectory;
    private readonly string _filePath;

    public JsonRecentStoriesStoreTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDirectory);
        _filePath = Path.Combine(_baseDirectory, "recent-stories.json");
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NothingHasBeenRecorded()
    {
        var store = NewStore();

        var recent = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(recent);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_TheFileIsCorrupt()
    {
        // A damaged settings file must not stop the editor from opening stories.
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(_filePath, "{ not json", cancellationToken);
        var store = NewStore();

        var recent = await store.ListAsync(cancellationToken);

        Assert.Empty(recent);
    }

    [Fact]
    public async Task Should_RecordTheStory_When_Recorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore(Noon);

        await store.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);

        var recent = Assert.Single(await store.ListAsync(cancellationToken));
        Assert.Equal("The Wreck", recent.Title);
        Assert.Equal(@"C:\stories\wreck", recent.FolderPath);
        Assert.Equal(Noon, recent.LastOpenedUtc);
    }

    [Fact]
    public async Task Should_WriteOnlyTheStoryFields_When_Recorded()
    {
        // The file is a persisted format a person can read and edit: it carries the story's own
        // fields and nothing the implementation happens to compute.
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore();

        await store.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_filePath, cancellationToken));
        var names = document.RootElement[0].EnumerateObject().Select(property => property.Name);
        Assert.Equal(["Title", "FolderPath", "LastOpenedUtc"], names);
    }

    [Fact]
    public async Task Should_ListMostRecentlyOpenedFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore();

        await store.RecordAsync(@"C:\stories\first", "First", cancellationToken);
        await store.RecordAsync(@"C:\stories\second", "Second", cancellationToken);

        var recent = await store.ListAsync(cancellationToken);
        Assert.Equal(["Second", "First"], recent.Select(story => story.Title));
    }

    [Fact]
    public async Task Should_ReplaceTheEarlierEntry_When_TheSameFolderIsRecordedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore();
        await store.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);
        await store.RecordAsync(@"C:\stories\other", "Other", cancellationToken);

        await store.RecordAsync(@"C:\stories\wreck", "The Wreck, Renamed", cancellationToken);

        var recent = await store.ListAsync(cancellationToken);
        Assert.Equal(2, recent.Count);
        Assert.Equal("The Wreck, Renamed", recent[0].Title);
        Assert.Equal(@"C:\stories\wreck", recent[0].FolderPath);
    }

    [Fact]
    public async Task Should_KeepOnlyTheTenMostRecent_When_MoreAreRecorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore();

        for (var i = 1; i <= 12; i++)
        {
            await store.RecordAsync($@"C:\stories\{i}", $"Story {i}", cancellationToken);
        }

        var recent = await store.ListAsync(cancellationToken);
        Assert.Equal(10, recent.Count);
        Assert.Equal("Story 12", recent[0].Title);
        Assert.Equal("Story 3", recent[^1].Title);
    }

    [Fact]
    public async Task Should_ForgetTheStory_When_Removed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore();
        await store.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);

        await store.RemoveAsync(@"C:\stories\wreck", cancellationToken);

        Assert.Empty(await store.ListAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_DoNothing_When_RemovingAnUnknownFolder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = NewStore();
        await store.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);

        await store.RemoveAsync(@"C:\stories\unknown", cancellationToken);

        Assert.Single(await store.ListAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_SurviveAcrossInstances_When_Recorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await NewStore().RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);

        var recent = await NewStore().ListAsync(cancellationToken);

        Assert.Equal("The Wreck", Assert.Single(recent).Title);
    }

    [Fact]
    public async Task Should_CreateTheParentFolder_When_ItIsMissing()
    {
        // The store's home is under the user's application data, which need not exist yet.
        var cancellationToken = TestContext.Current.CancellationToken;
        var nested = Path.Combine(_baseDirectory, "Fourthwall", "recent-stories.json");
        var store = new JsonRecentStoriesStore(nested);

        await store.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);

        Assert.True(File.Exists(nested));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_RecordedFolderIsBlank(string folderPath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => NewStore().RecordAsync(folderPath, "The Wreck", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_RecordedTitleIsBlank(string title)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => NewStore().RecordAsync(@"C:\stories\wreck", title, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_RemovedFolderIsBlank(string folderPath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => NewStore().RemoveAsync(folderPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Throw_When_FilePathIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonRecentStoriesStore(null!));
    }

    [Fact]
    public void Should_Throw_When_TimeProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonRecentStoriesStore(_filePath, null!));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle during teardown must not fail the test; the temp folder is disposable.
        }
    }

    private JsonRecentStoriesStore NewStore() => new(_filePath);

    private JsonRecentStoriesStore NewStore(DateTimeOffset now) =>
        new(_filePath, new FixedTimeProvider(now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
