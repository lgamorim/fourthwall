using Bunit.TestDoubles;

using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Pages;

using System.Text.Json;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fourthwall.Web.UnitTests;

public class HomeTests : BunitContext
{
    private const string RecentStateKey = "recent-stories";

    private readonly FakeStoryWorkspace _workspace = new();
    private readonly FakeRecentStories _recent = new();

    // The host registers PersistentComponentState as part of AddRazorComponents; bUnit does not, so
    // the page under test gets one built the same way the framework builds it. Held here so a test
    // can seed it and take the prerender-handoff path.
    private readonly ComponentStatePersistenceManager _persistence =
        new(NullLogger<ComponentStatePersistenceManager>.Instance);

    public HomeTests()
    {
        Services.AddSingleton<IStoryWorkspace>(_workspace);
        Services.AddSingleton<IRecentStories>(_recent);
        Services.AddSingleton(_persistence.State);
    }

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    [Fact]
    public async Task Should_UseThePersistedList_When_ThePrerenderHandedOneOver()
    {
        // Arrange — prerendering renders the page twice per visit. The second pass takes the list
        // the first pass persisted instead of reading it off disk again.
        await SeedPersistedRecentAsync(
            new RecentStory("Persisted", @"C:\stories\persisted", DateTimeOffset.UnixEpoch));

        // Act
        var cut = Render<Home>();

        // Assert
        Assert.Equal("Persisted", cut.Find(".recent-open").TextContent.Trim());
    }

    [Fact]
    public async Task Should_NotReadFromDisk_When_ThePrerenderHandedTheListOver()
    {
        // Arrange — the whole point of the handoff is one read per visit, not two. The store holds
        // a different story, so reading it would be visible.
        await _recent.RecordAsync(@"C:\storiesrom-disk", "From disk", TestContext.Current.CancellationToken);
        await SeedPersistedRecentAsync(
            new RecentStory("Persisted", @"C:\stories\persisted", DateTimeOffset.UnixEpoch));

        // Act
        var cut = Render<Home>();

        // Assert
        Assert.Equal(["Persisted"], cut.FindAll(".recent-open").Select(entry => entry.TextContent.Trim()));
    }



    [Fact]
    public void Should_CreateAndOpenTheStory_When_CreateIsSubmitted()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#create-folder").Change(@"C:\stories\wreck");
        cut.Find("#create-title").Change("The Wreck");

        // Act
        cut.Find("#create-story").Submit();

        // Assert
        Assert.Equal("The Wreck", _workspace.Current?.Title);
        Assert.Equal(@"C:\stories\wreck", _workspace.FolderPath);
    }

    [Fact]
    public async Task Should_RememberTheStory_When_Created()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#create-folder").Change(@"C:\stories\wreck");
        cut.Find("#create-title").Change("The Wreck");

        // Act
        cut.Find("#create-story").Submit();

        // Assert
        var remembered = await _recent.ListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(@"C:\stories\wreck", Assert.Single(remembered).FolderPath);
    }

    [Fact]
    public void Should_ShowAnError_When_CreatingWhereAStoryAlreadyExists()
    {
        // Arrange
        _workspace.Stories[@"C:\stories\wreck"] = new Story("Existing");
        var cut = Render<Home>();
        cut.Find("#create-folder").Change(@"C:\stories\wreck");
        cut.Find("#create-title").Change("The Wreck");

        // Act
        cut.Find("#create-story").Submit();

        // Assert — an operational failure, so it lands on the error line and not on a field.
        Assert.Contains("already exists", cut.Find(".picker-error").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".validation-message"));
    }

    [Fact]
    public void Should_DropTheOperationalError_When_TheNextSubmitIsInvalid()
    {
        // Arrange — a failed create leaves "already exists" on the error line. Clearing the folder
        // and submitting again is answered by the form, which never reaches the workspace, so the
        // old line would otherwise sit above the new message describing an operation nobody is
        // still attempting.
        _workspace.Stories[@"C:\stories\wreck"] = new Story("Existing");
        var cut = Render<Home>();
        cut.Find("#create-folder").Change(@"C:\stories\wreck");
        cut.Find("#create-title").Change("The Wreck");
        cut.Find("#create-story").Submit();
        Assert.NotNull(cut.Find(".picker-error"));

        // Act
        cut.Find("#create-folder").Change(string.Empty);
        cut.Find("#create-story").Submit();

        // Assert
        Assert.Empty(cut.FindAll(".picker-error"));
        Assert.NotEmpty(cut.FindAll(".validation-message"));
    }

    [Fact]
    public void Should_ShowAValidationMessage_When_CreateIsSubmittedWithoutAFolder()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#create-title").Change("The Wreck");

        // Act
        cut.Find("#create-story").Submit();

        // Assert — shape is the form's business, so this never reaches the workspace.
        Assert.Contains(
            "Enter the folder to create the story in.",
            cut.Find(".validation-message").TextContent,
            StringComparison.Ordinal);
        Assert.Null(_workspace.Current);
    }

    [Fact]
    public void Should_ShowAValidationMessage_When_CreateIsSubmittedWithoutATitle()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#create-folder").Change(@"C:\stories\wreck");

        // Act
        cut.Find("#create-story").Submit();

        // Assert
        Assert.Contains(
            "Enter a title for the story.",
            cut.Find(".validation-message").TextContent,
            StringComparison.Ordinal);
        Assert.Null(_workspace.Current);
    }

    [Fact]
    public void Should_ShowAValidationMessage_When_OpenIsSubmittedWithoutAFolder()
    {
        // Arrange
        var cut = Render<Home>();

        // Act
        cut.Find("#open-story").Submit();

        // Assert
        Assert.Contains(
            "Enter the folder holding the story.",
            cut.Find(".validation-message").TextContent,
            StringComparison.Ordinal);
        Assert.Null(_workspace.Current);
    }

    [Fact]
    public void Should_OpenTheStory_When_OpenIsSubmitted()
    {
        // Arrange
        _workspace.Stories[@"C:\stories\wreck"] = new Story("The Wreck");
        var cut = Render<Home>();
        cut.Find("#open-folder").Change(@"C:\stories\wreck");

        // Act
        cut.Find("#open-story").Submit();

        // Assert
        Assert.Equal("The Wreck", _workspace.Current?.Title);
    }

    [Fact]
    public void Should_ShowAnError_When_OpeningWhereNoStoryExists()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#open-folder").Change(@"C:\stories\missing");

        // Act
        cut.Find("#open-story").Submit();

        // Assert
        Assert.Contains("No story", cut.Find(".picker-error").TextContent, StringComparison.Ordinal);
        Assert.Null(_workspace.Current);
    }

    [Fact]
    public async Task Should_ListRememberedStories_When_Rendered()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _recent.RecordAsync(@"C:\stories\first", "First", cancellationToken);
        await _recent.RecordAsync(@"C:\stories\second", "Second", cancellationToken);

        // Act
        var cut = Render<Home>();

        // Assert — most recently opened first.
        var titles = cut.FindAll(".recent-open").Select(entry => entry.TextContent.Trim());
        Assert.Equal(["Second", "First"], titles);
    }

    [Fact]
    public async Task Should_OpenTheStory_When_ARememberedEntryIsChosen()
    {
        // Arrange
        _workspace.Stories[@"C:\stories\wreck"] = new Story("The Wreck");
        await _recent.RecordAsync(@"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken);
        var cut = Render<Home>();

        // Act
        cut.Find(".recent-open").Click();

        // Assert
        Assert.Equal("The Wreck", _workspace.Current?.Title);
    }

    [Fact]
    public async Task Should_MarkTheEntryUnavailable_When_ItCannotBeOpened()
    {
        // Arrange — remembered, but the folder is gone.
        await _recent.RecordAsync(@"C:\stories\gone", "Gone", TestContext.Current.CancellationToken);
        var cut = Render<Home>();

        // Act
        cut.Find(".recent-open").Click();

        // Assert
        Assert.NotNull(cut.Find(".recent-unavailable"));
        Assert.Null(_workspace.Current);
    }

    [Fact]
    public async Task Should_ForgetTheEntry_When_RemoveIsChosen()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _recent.RecordAsync(@"C:\stories\wreck", "The Wreck", cancellationToken);
        var cut = Render<Home>();

        // Act
        cut.Find(".recent-remove").Click();

        // Assert
        Assert.Empty(await _recent.ListAsync(cancellationToken));
        Assert.Empty(cut.FindAll(".recent-open"));
    }

    [Fact]
    public void Should_OpenTheEditor_When_AStoryIsCreated()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#create-folder").Change(@"C:\stories\wreck");
        cut.Find("#create-title").Change("The Wreck");

        // Act
        cut.Find("#create-story").Submit();

        // Assert
        Assert.Equal("/story", Assert.Single(Navigation.History).Uri);
    }

    [Fact]
    public void Should_OpenTheEditor_When_AStoryIsOpened()
    {
        // Arrange
        _workspace.Stories[@"C:\stories\wreck"] = new Story("The Wreck");
        var cut = Render<Home>();
        cut.Find("#open-folder").Change(@"C:\stories\wreck");

        // Act
        cut.Find("#open-story").Submit();

        // Assert
        Assert.Equal("/story", Assert.Single(Navigation.History).Uri);
    }

    [Fact]
    public void Should_StayOnThePicker_When_OpeningFails()
    {
        // Arrange
        var cut = Render<Home>();
        cut.Find("#open-folder").Change(@"C:\stories\missing");

        // Act
        cut.Find("#open-story").Submit();

        // Assert
        Assert.Empty(Navigation.History);
    }

    [Fact]
    public async Task Should_StopNamingTheStory_When_ItIsClosedElsewhere()
    {
        // Arrange — the header owns the close action, so the picker has to react to a change it
        // did not cause.
        var cancellationToken = TestContext.Current.CancellationToken;
        _workspace.Stories[@"C:\stories\wreck"] = new Story("The Wreck");
        await _workspace.OpenAsync(@"C:\stories\wreck", cancellationToken);
        var cut = Render<Home>();

        // Act
        await _workspace.CloseAsync(cancellationToken);

        // Assert
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".picker-open")));
    }

    [Fact]
    public async Task Should_StopListening_When_Disposed()
    {
        // Arrange
        Render<Home>();

        // Act
        await DisposeComponentsAsync();

        // Assert — a disposed component that is still subscribed would throw when told to render.
        var exception = await Record.ExceptionAsync(
            () => _workspace.CreateAsync(@"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken));
        Assert.Null(exception);
    }

    [Fact]
    public void Should_NameTheOpenStory_When_OneIsOpen()
    {
        // Arrange
        _workspace.Stories[@"C:\stories\wreck"] = new Story("The Wreck");
        var cut = Render<Home>();
        cut.Find("#open-folder").Change(@"C:\stories\wreck");

        // Act
        cut.Find("#open-story").Submit();

        // Assert
        var open = cut.Find(".picker-open").TextContent;
        Assert.Contains("The Wreck", open, StringComparison.Ordinal);
        Assert.Contains(@"C:\stories\wreck", open, StringComparison.Ordinal);
    }

    // Seeds the state a prerender would have handed over, serialized the way the framework does.
    //
    // Only the restore half of the handoff is exercised here: PersistStateAsync needs a Renderer,
    // which bUnit does not surface, so the persisting callback itself stays an accepted boundary
    // (overlays/frontend-blazor.md). What that callback writes is the same list this reads back.
    private Task SeedPersistedRecentAsync(params RecentStory[] stories) =>
        _persistence.RestoreStateAsync(new SeededStore(new Dictionary<string, byte[]>
        {
            [RecentStateKey] = JsonSerializer.SerializeToUtf8Bytes(
                (IReadOnlyList<RecentStory>)stories, JsonSerializerOptions.Web),
        }));

    private sealed class SeededStore(IDictionary<string, byte[]> state) : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(state);

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> instance) => Task.CompletedTask;
    }
}
