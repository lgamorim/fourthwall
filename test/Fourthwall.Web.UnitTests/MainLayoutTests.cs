using Fourthwall.Web.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fourthwall.Web.UnitTests;

public class MainLayoutTests : BunitContext
{
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

    private static void EmptyBody(RenderTreeBuilder builder)
    {
    }
}
