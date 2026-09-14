using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class SceneEdgeTests : BunitContext
{
    [Fact]
    public void Should_DrawALabelledChoiceLink_When_TheEdgeIsAChoice()
    {
        // Arrange
        var edge = Edge(choiceIndex: 0, label: "Go below deck");

        // Act
        var cut = RenderEdge(edge);

        // Assert
        Assert.Contains("edge-choice", cut.Find(".canvas-edge").ClassList);
        Assert.Equal("Go below deck", cut.Find(".edge-label").TextContent);
    }

    [Fact]
    public void Should_DrawAnUnlabelledFollowUp_When_TheEdgeIsAFollowUp()
    {
        // Arrange
        var edge = Edge(choiceIndex: null, label: string.Empty);

        // Act
        var cut = RenderEdge(edge);

        // Assert — no decision, so no label; the dotted line is the stylesheet's, keyed on the class.
        Assert.Contains("edge-follow-up", cut.Find(".canvas-edge").ClassList);
        Assert.Empty(cut.FindAll(".edge-label"));
    }

    [Fact]
    public void Should_FollowTheModelsCurve_When_Rendered()
    {
        // Arrange
        var edge = Edge(choiceIndex: 0, label: "Go");

        // Act
        var cut = RenderEdge(edge);

        // Assert
        var line = cut.Find(".edge-line");
        Assert.Equal(edge.PathData, line.GetAttribute("d"));
        Assert.Equal("url(#canvas-arrow)", line.GetAttribute("marker-end"));
    }

    [Fact]
    public void Should_PlaceTheLabelAtTheModelsPoint_When_Rendered()
    {
        // Arrange
        var edge = Edge(choiceIndex: 0, label: "Go");

        // Act
        var cut = RenderEdge(edge);

        // Assert
        var label = cut.Find(".edge-label");
        Assert.Equal(edge.LabelX, label.GetAttribute("x"));
        Assert.Equal(edge.LabelY, label.GetAttribute("y"));
        Assert.Equal("middle", label.GetAttribute("text-anchor"));
    }

    [Fact]
    public void Should_SetTheLabelBesideTheLoop_When_TheEdgeIsASelfLoop()
    {
        // Arrange — the self-loop's label point is the loop's outermost point, so the label starts
        // there rather than centring across the loop.
        var edge = Edge(choiceIndex: 0, label: "Hold on") with { IsSelfLoop = true };

        // Act
        var cut = RenderEdge(edge);

        // Assert
        Assert.Equal("start", cut.Find(".edge-label").GetAttribute("text-anchor"));
    }

    [Fact]
    public void Should_CutTheLabelAndKeepItWholeInTheTitle_When_TheLabelIsLong()
    {
        // Arrange
        const string label = "Swim for the lifeboat before it drifts";
        var edge = Edge(choiceIndex: 0, label: label);

        // Act
        var cut = RenderEdge(edge);

        // Assert
        Assert.Equal("Swim for the lif…", cut.Find(".edge-label").TextContent);
        Assert.Equal(label, cut.Find(".canvas-edge > title").TextContent);
    }

    private static CanvasEdge Edge(int? choiceIndex, string label)
    {
        var source = SceneId.New();
        return new CanvasEdge(
            new CanvasEdgeKey(source, choiceIndex), source, SceneId.New(), label,
            ParallelIndex: 0, IsSelfLoop: false, PathData: "M 240,72 C 300,72 300,72 360,72",
            LabelX: "300", LabelY: "62");
    }

    private IRenderedComponent<SvgHost> RenderEdge(CanvasEdge edge) =>
        Render<SvgHost>(host => host.AddChildContent<SceneEdge>(parameters => parameters.Add(p => p.Edge, edge)));
}
