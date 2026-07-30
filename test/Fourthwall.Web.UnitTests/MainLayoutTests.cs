using Fourthwall.Application;
using Fourthwall.Web.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class MainLayoutTests : BunitContext
{
    private readonly FakeStoryWorkspace _workspace = new();

    public MainLayoutTests()
    {
        Services.AddSingleton<IStoryWorkspace>(_workspace);
    }

    [Fact]
    public void Should_RenderAppHeader_When_LayoutIsRendered()
    {
        // Arrange & Act
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Assert
        var header = cut.Find("header");
        Assert.Contains("Fourthwall", header.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RenderBodyInsideMainRegion_When_BodyIsProvided()
    {
        // Arrange
        RenderFragment body = builder => builder.AddMarkupContent(0, "<p>scene text</p>");

        // Act
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, body));

        // Assert
        var main = cut.Find("main");
        Assert.Contains("scene text", main.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_RenderDock_When_LayoutIsRendered()
    {
        // Arrange & Act
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Assert — the dock is the surface M13's inspector and M16's validation panel occupy.
        Assert.NotNull(cut.Find("aside"));
    }

    [Fact]
    public async Task Should_NameTheOpenStory_When_OneIsOpen()
    {
        // Arrange
        await OpenStoryAsync("The Wreck");

        // Act
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Assert
        Assert.Equal("The Wreck", cut.Find(".story-title").TextContent.Trim());
    }

    [Fact]
    public void Should_OfferNoStoryActions_When_NoStoryIsOpen()
    {
        // Arrange & Act
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Assert
        Assert.Empty(cut.FindAll(".story-title"));
        Assert.Empty(cut.FindAll("#close-story"));
    }

    [Fact]
    public async Task Should_CloseTheStory_When_CloseIsChosen()
    {
        // Arrange
        await OpenStoryAsync("The Wreck");
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Act
        cut.Find("#close-story").Click();

        // Assert
        Assert.Null(_workspace.Current);
        Assert.Empty(cut.FindAll(".story-title"));
    }

    [Fact]
    public async Task Should_NameTheStory_When_ItIsOpenedAfterRendering()
    {
        // Arrange — the picker opens the story, so the header must react to a change it did not
        // cause.
        var cut = Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Act
        await OpenStoryAsync("The Wreck");

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("The Wreck", cut.Find(".story-title").TextContent.Trim()));
    }

    [Fact]
    public async Task Should_StopListening_When_Disposed()
    {
        // Arrange
        Render<MainLayout>(parameters => parameters.Add(p => p.Body, EmptyBody));

        // Act
        await DisposeComponentsAsync();

        // Assert — a disposed component that is still subscribed would throw when told to render.
        var exception = await Record.ExceptionAsync(() => OpenStoryAsync("The Wreck"));
        Assert.Null(exception);
    }

    private static void EmptyBody(RenderTreeBuilder builder)
    {
    }

    private Task OpenStoryAsync(string title) =>
        _workspace.CreateAsync($@"C:\stories\{title}", title, TestContext.Current.CancellationToken);
}
