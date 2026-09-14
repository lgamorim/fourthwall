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

    private IRenderedComponent<SvgHost> RenderEdge(CanvasEdge edge) =>
        Render<SvgHost>(host => host.AddChildContent<SceneEdge>(parameters => parameters.Add(p => p.Edge, edge)));
}
