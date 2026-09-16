using Fourthwall.Application;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class SceneEdgeLabelTests : BunitContext
{
    [Fact]
    public void Should_ShowTheChoicesLabel_When_TheEdgeIsAChoice()
    {
        // Arrange
        var edge = CanvasEdges.Choice("Go below deck");

        // Act
        var cut = RenderLabel(edge);

        // Assert
        Assert.Equal("Go below deck", cut.Find(".edge-label-text").TextContent);
    }

    [Fact]
    public void Should_ShowNothing_When_TheEdgeIsAFollowUp()
    {
        // Arrange
        var edge = CanvasEdges.FollowUp();

        // Act
        var cut = RenderLabel(edge);

        // Assert — no decision, so no label.
        Assert.Empty(cut.FindAll(".edge-label"));
    }

    [Fact]
    public void Should_PlaceTheLabelAtTheModelsPoint_When_Rendered()
    {
        // Arrange
        var edge = CanvasEdges.Choice("Go");

        // Act
        var cut = RenderLabel(edge);

        // Assert
        var label = cut.Find(".edge-label");
        Assert.Equal(edge.LabelX, label.GetAttribute("x"));
        Assert.Equal(edge.LabelY, label.GetAttribute("y"));
        Assert.Equal("middle", label.GetAttribute("text-anchor"));
    }

    [Fact]
    public void Should_SetTheLabelBesideTheLoop_When_TheEdgeIsASelfLoop()
    {
        // Arrange — the self-loop's label point is beside the top of the loop, so the label starts
        // there rather than centring across it.
        var edge = CanvasEdges.Choice("Hold on") with { IsSelfLoop = true };

        // Act
        var cut = RenderLabel(edge);

        // Assert
        Assert.Equal("start", cut.Find(".edge-label").GetAttribute("text-anchor"));
    }

    [Fact]
    public void Should_CutTheLabelAndKeepItWholeInTheTitle_When_TheLabelIsLong()
    {
        // Arrange
        const string label = "Swim for the lifeboat before it drifts";
        var edge = CanvasEdges.Choice(label);

        // Act
        var cut = RenderLabel(edge);

        // Assert
        Assert.Equal("Swim for the lif…", cut.Find(".edge-label-text").TextContent);
        Assert.Equal(label, cut.Find(".edge-label > title").TextContent);
    }

    [Fact]
    public void Should_TurnRibbon_When_TheLinkIsSelected()
    {
        // Arrange
        var edge = CanvasEdges.Choice("Go");

        // Act
        var cut = RenderLabel(edge, isSelected: true);

        // Assert
        Assert.Contains("edge-label-selected", cut.Find(".edge-label").ClassList);
    }

    [Fact]
    public void Should_RaiseSelection_When_TheLabelIsClicked()
    {
        // Arrange — the label is the largest part of a link to click.
        var selected = 0;
        var cut = RenderLabel(CanvasEdges.Choice("Go"), onSelected: () => selected++);

        // Act
        cut.Find(".edge-label").Click();

        // Assert
        Assert.Equal(1, selected);
    }

    [Theory]
    [InlineData(ValidationSeverity.Error, "edge-mark-error", "#canvas-mark-error")]
    [InlineData(ValidationSeverity.Warning, "edge-mark-warning", "#canvas-mark-warning")]
    public void Should_MarkTheCurve_When_AFollowUpsSceneHasAProblem(
        ValidationSeverity severity, string expectedClass, string expectedMark)
    {
        // Arrange
        var edge = CanvasEdges.FollowUp();

        // Act
        var cut = RenderLabel(edge, severity: severity);

        // Assert — a follow-up has no label, so the mark stands alone on its line (§13.3).
        var mark = cut.Find(".edge-mark");
        Assert.Contains(expectedClass, mark.ClassList);
        Assert.Equal($"translate({edge.MarkX} {edge.MarkY})", mark.GetAttribute("transform"));
        Assert.Equal(expectedMark, cut.Find(".edge-mark use").GetAttribute("href"));
        Assert.Empty(cut.FindAll(".edge-label"));
    }

    [Fact]
    public void Should_MarkTheCurveAndKeepTheLabel_When_AChoicesSceneHasAProblem()
    {
        // Act
        var cut = RenderLabel(CanvasEdges.Choice("Go below deck"), severity: ValidationSeverity.Warning);

        // Assert — the label stays pencil; the mark says what is wrong.
        Assert.NotNull(cut.Find(".edge-mark"));
        Assert.Equal(["edge-label"], cut.Find(".edge-label").ClassList);
    }

    [Fact]
    public void Should_MarkNothing_When_TheReportBlamesNothing()
    {
        // Act
        var cut = RenderLabel(CanvasEdges.Choice("Go"));

        // Assert
        Assert.Empty(cut.FindAll(".edge-mark"));
    }

    private IRenderedComponent<SvgHost> RenderLabel(
        CanvasEdge edge, bool isSelected = false, ValidationSeverity? severity = null, Action? onSelected = null) =>
        Render<SvgHost>(host => host.AddChildContent<SceneEdgeLabel>(parameters => parameters
            .Add(p => p.Edge, edge)
            .Add(p => p.IsSelected, isSelected)
            .Add(p => p.Severity, severity)
            .Add(p => p.OnSelected, onSelected ?? (() => { }))));
}
