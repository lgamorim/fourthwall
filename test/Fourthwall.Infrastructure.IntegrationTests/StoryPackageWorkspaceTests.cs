using Fourthwall.Domain;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class StoryPackageWorkspaceTests : IDisposable
{
    private readonly string _baseDirectory;

    public StoryPackageWorkspaceTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDirectory);
    }

    [Fact]
    public async Task Should_OpenTheNewStory_When_Created()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        await using var workspace = new StoryPackageWorkspace();

        var story = await workspace.CreateAsync(folder, "The Wreck", cancellationToken);

        Assert.Equal("The Wreck", story.Title);
        Assert.Same(story, workspace.Current);
        Assert.Equal(folder, workspace.FolderPath);
        Assert.True(File.Exists(Path.Combine(folder, "story.db")));
    }

    [Fact]
    public async Task Should_PersistTheNewStory_When_Created()
    {
        // A created folder must never be a story database with no story in it.
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();

        await using (var workspace = new StoryPackageWorkspace())
        {
            await workspace.CreateAsync(folder, "The Wreck", cancellationToken);
        }

        await using var reopened = new StoryPackageWorkspace();
        var story = await reopened.OpenAsync(folder, cancellationToken);
        Assert.Equal("The Wreck", story.Title);
    }

    [Fact]
    public async Task Should_RoundTripEdits_When_SavedAndReopened()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        SceneId sceneId;

        await using (var workspace = new StoryPackageWorkspace())
        {
            var story = await workspace.CreateAsync(folder, "The Wreck", cancellationToken);
            var ending = story.AddScene(SceneKind.Ending, "You drown.", EndingOutcome.Death());
            story.SetStartScene(ending.Id);
            sceneId = ending.Id;
            await workspace.SaveAsync(cancellationToken);
            await workspace.CloseAsync(cancellationToken);
        }

        await using var reopened = new StoryPackageWorkspace();
        var loaded = await reopened.OpenAsync(folder, cancellationToken);
        Assert.Equal(sceneId, loaded.StartSceneId);
        Assert.Equal("You drown.", loaded.FindScene(sceneId)!.Text);
    }

    [Fact]
    public async Task Should_Throw_When_CreatingWhereStoryAlreadyExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        await using var workspace = new StoryPackageWorkspace();
        await workspace.CreateAsync(folder, "The Wreck", cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => workspace.CreateAsync(folder, "Another", cancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_OpeningWhereNoStoryExists()
    {
        await using var workspace = new StoryPackageWorkspace();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => workspace.OpenAsync(NewStoryFolder(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_OpeningADatabaseWithNoStory()
    {
        // A story.db with no story row is a broken folder, not an empty editor.
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        await using (await StoryPackage.CreateAsync(folder, cancellationToken))
        {
        }

        await using var workspace = new StoryPackageWorkspace();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => workspace.OpenAsync(folder, cancellationToken));
    }

    [Fact]
    public async Task Should_ReplaceThePreviousStory_When_AnotherIsOpened()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = NewStoryFolder();
        var second = NewStoryFolder();
        await using var workspace = new StoryPackageWorkspace();
        await workspace.CreateAsync(first, "First", cancellationToken);

        await workspace.CreateAsync(second, "Second", cancellationToken);

        Assert.Equal("Second", workspace.Current!.Title);
        Assert.Equal(second, workspace.FolderPath);
    }

    [Fact]
    public async Task Should_ReleaseThePreviousFolder_When_AnotherIsOpened()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = NewStoryFolder();
        await using var workspace = new StoryPackageWorkspace();
        await workspace.CreateAsync(first, "First", cancellationToken);

        await workspace.CreateAsync(NewStoryFolder(), "Second", cancellationToken);

        var exception = Record.Exception(() => Directory.Delete(first, recursive: true));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_ForgetTheStory_When_Closed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var workspace = new StoryPackageWorkspace();
        await workspace.CreateAsync(NewStoryFolder(), "The Wreck", cancellationToken);

        await workspace.CloseAsync(cancellationToken);

        Assert.Null(workspace.Current);
        Assert.Null(workspace.FolderPath);
    }

    [Fact]
    public async Task Should_ReleaseTheFolder_When_Closed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        await using var workspace = new StoryPackageWorkspace();
        await workspace.CreateAsync(folder, "The Wreck", cancellationToken);

        await workspace.CloseAsync(cancellationToken);

        var exception = Record.Exception(() => Directory.Delete(folder, recursive: true));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_DoNothing_When_ClosedWithNoStoryOpen()
    {
        await using var workspace = new StoryPackageWorkspace();

        var exception = await Record.ExceptionAsync(
            () => workspace.CloseAsync(TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_Throw_When_SavingWithNoStoryOpen()
    {
        await using var workspace = new StoryPackageWorkspace();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => workspace.SaveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_RaiseChanged_When_TheOpenStoryChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var workspace = new StoryPackageWorkspace();
        var raised = 0;
        workspace.Changed += (_, _) => raised++;

        await workspace.CreateAsync(NewStoryFolder(), "The Wreck", cancellationToken);
        await workspace.SaveAsync(cancellationToken);
        await workspace.CloseAsync(cancellationToken);

        Assert.Equal(3, raised);
    }

    [Fact]
    public async Task Should_NotRaiseChanged_When_ClosedWithNoStoryOpen()
    {
        await using var workspace = new StoryPackageWorkspace();
        var raised = 0;
        workspace.Changed += (_, _) => raised++;

        await workspace.CloseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Should_ReleaseTheFolder_When_Disposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = NewStoryFolder();
        var workspace = new StoryPackageWorkspace();
        await workspace.CreateAsync(folder, "The Wreck", cancellationToken);

        await workspace.DisposeAsync();

        var exception = Record.Exception(() => Directory.Delete(folder, recursive: true));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_CreateFolderIsBlank(string folder)
    {
        await using var workspace = new StoryPackageWorkspace();

        await Assert.ThrowsAsync<ArgumentException>(
            () => workspace.CreateAsync(folder, "The Wreck", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_CreateTitleIsBlank(string title)
    {
        await using var workspace = new StoryPackageWorkspace();

        await Assert.ThrowsAsync<ArgumentException>(
            () => workspace.CreateAsync(NewStoryFolder(), title, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_NotCreateTheFolder_When_TitleIsBlank()
    {
        // The title is rejected before anything touches the disk, so a failed create leaves no
        // half-made story behind to block the retry.
        var folder = NewStoryFolder();
        await using var workspace = new StoryPackageWorkspace();

        await Assert.ThrowsAsync<ArgumentException>(
            () => workspace.CreateAsync(folder, "  ", TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(folder));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_OpenFolderIsBlank(string folder)
    {
        await using var workspace = new StoryPackageWorkspace();

        await Assert.ThrowsAsync<ArgumentException>(
            () => workspace.OpenAsync(folder, TestContext.Current.CancellationToken));
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
