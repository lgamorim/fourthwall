using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class SceneEdgeTests : BunitContext
{
    [Fact]
    public void Should_DrawAChoiceLink_When_TheEdgeIsAChoice()
    {
        // Arrange
        var edge = CanvasEdges.Choice("Go below deck");

        // Act
        var cut = RenderEdge(edge);

        // Assert — the line only: labels are drawn in their own layer, over every line.
        Assert.Contains("edge-choice", cut.Find(".canvas-edge").ClassList);
        Assert.Equal("Go below deck", cut.Find(".canvas-edge > title").TextContent);
        Assert.Empty(cut.FindAll(".edge-label"));
    }

    [Fact]
    public void Should_DrawAFollowUp_When_TheEdgeIsAFollowUp()
    {
        // Arrange
        var edge = CanvasEdges.FollowUp();

        // Act
        var cut = RenderEdge(edge);

        // Assert — the dotted line is the stylesheet's, keyed on the class; there is nothing to title.
        Assert.Contains("edge-follow-up", cut.Find(".canvas-edge").ClassList);
        Assert.Empty(cut.FindAll("title"));
    }

    [Fact]
    public void Should_FollowTheModelsCurve_When_Rendered()
    {
        // Arrange
        var edge = CanvasEdges.Choice("Go");

        // Act
        var cut = RenderEdge(edge);

        // Assert
        var line = cut.Find(".edge-line");
        Assert.Equal(edge.PathData, line.GetAttribute("d"));
        Assert.Equal("url(#canvas-arrow)", line.GetAttribute("marker-end"));
    }

    [Fact]
    public void Should_TurnRibbon_When_Selected()
    {
        // Arrange
        var edge = CanvasEdges.Choice("Go");

        // Act
        var cut = RenderEdge(edge, isSelected: true);

        // Assert — the class colours the line; the marker is the selected one.
        Assert.Contains("edge-selected", cut.Find(".canvas-edge").ClassList);
        Assert.Equal("url(#canvas-arrow-selected)", cut.Find(".edge-line").GetAttribute("marker-end"));
    }

    [Fact]
    public void Should_NotBeMarked_When_NotSelected()
    {
        // Arrange
        var edge = CanvasEdges.FollowUp();

        // Act
        var cut = RenderEdge(edge);

        // Assert
        Assert.DoesNotContain("edge-selected", cut.Find(".canvas-edge").ClassList);
    }

    [Fact]
    public void Should_RaiseSelection_When_TheLineIsClicked()
    {
        // Arrange — a 1.5px line is hard to hit, so a wider invisible stroke along it takes the click.
        var selected = 0;
        var edge = CanvasEdges.Choice("Go");
        var cut = RenderEdge(edge, onSelected: () => selected++);

        // Act
        cut.Find(".edge-hit").Click();

        // Assert
        Assert.Equal(1, selected);
        Assert.Equal(edge.PathData, cut.Find(".edge-hit").GetAttribute("d"));
    }

    private IRenderedComponent<SvgHost> RenderEdge(CanvasEdge edge, bool isSelected = false, Action? onSelected = null) =>
        Render<SvgHost>(host => host.AddChildContent<SceneEdge>(parameters => parameters
            .Add(p => p.Edge, edge)
            .Add(p => p.IsSelected, isSelected)
            .Add(p => p.OnSelected, onSelected ?? (() => { }))));
}
