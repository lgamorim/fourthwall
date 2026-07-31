using Fourthwall.Application;
using Fourthwall.Domain;

using Bunit.TestDoubles;

using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class StoryEditorTests : BunitContext
{
    private readonly FakeStoryWorkspace _workspace = new();

    public StoryEditorTests()
    {
        Services.AddSingleton<IStoryWorkspace>(_workspace);
    }

    [Fact]
    public void Should_ReturnToThePicker_When_NoStoryIsOpen()
    {
        // Arrange & Act
        RenderEditor();

        // Assert
        Assert.Equal("/", Assert.Single(Navigation.History).Uri);
    }

    [Fact]
    public async Task Should_ShowTheSceneList_When_AStoryIsOpen()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert
        Assert.NotNull(cut.Find(".scene-create"));
    }

    [Fact]
    public async Task Should_SaveTheStory_When_ASceneIsAdded()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A storm gathers");

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert — every committed mutation is persisted; there is no explicit save.
        Assert.Equal(1, _workspace.SaveCount);
    }

    [Fact]
    public async Task Should_RenameTheStory_When_ANewTitleIsCommitted()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = RenderEditor();

        // Act
        cut.Find("#story-title").Change("The Wreck of the Marianne");

        // Assert
        Assert.Equal("The Wreck of the Marianne", _workspace.Current!.Title);
        Assert.Equal(1, _workspace.SaveCount);
    }

    [Fact]
    public async Task Should_RefuseABlankTitle_When_ItIsCommitted()
    {
        // Arrange — Story.Rename rejects a blank title, so the page must not hand it one.
        await OpenStoryAsync();
        var cut = RenderEditor();

        // Act
        cut.Find("#story-title").Change("   ");

        // Assert
        Assert.Equal("The Wreck", _workspace.Current!.Title);
        Assert.NotNull(cut.Find(".editor-error"));
    }

    [Fact]
    public async Task Should_ShowTheInspector_When_ASceneIsSelected()
    {
        // Arrange
        await OpenStoryAsync();
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A storm gathers");
        cut.Find("#scene-create-submit").Click();

        // Act
        cut.Find(".scene-select").Click();

        // Assert — a newly added scene is selected, and the dock shows it.
        Assert.NotNull(cut.Find("#inspector-text"));
    }

    [Fact]
    public async Task Should_ShowNoInspector_When_NothingIsSelected()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var cut = RenderEditor();

        // Assert
        Assert.Empty(cut.FindAll("#inspector-text"));
    }

    [Fact]
    public async Task Should_ReportTheFailure_When_SavingThrows()
    {
        // Arrange
        await OpenStoryAsync();
        _workspace.FailNextSave = new IOException("The story folder is read-only.");
        var cut = RenderEditor();
        cut.Find("#scene-create-text").Change("A storm gathers");

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert
        Assert.Contains("read-only", cut.Find(".editor-error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_ReturnToThePicker_When_TheStoryIsClosed()
    {
        // Arrange
        await OpenStoryAsync();
        RenderEditor();

        // Act
        await _workspace.CloseAsync(TestContext.Current.CancellationToken);

        // Assert — the header can close the story while the editor is showing it.
        Assert.Equal("/", Assert.Single(Navigation.History).Uri);
    }

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private IRenderedComponent<DockHost> RenderEditor() => Render<DockHost>();

    private Task<Story> OpenStoryAsync() =>
        _workspace.CreateAsync(@"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken);
}
