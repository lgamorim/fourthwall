using Fourthwall.Application;
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

    [Theory]
    [InlineData(ValidationSeverity.Error, "edge-error", "url(#canvas-arrow-error)")]
    [InlineData(ValidationSeverity.Warning, "edge-warning", "url(#canvas-arrow-warning)")]
    public void Should_TakeItsScenesSeverity_When_TheReportBlamesTheScene(
        ValidationSeverity severity, string expectedClass, string expectedMarker)
    {
        // Act
        var cut = RenderEdge(CanvasEdges.FollowUp(), severity: severity);

        // Assert
        Assert.Contains(expectedClass, cut.Find(".canvas-edge").ClassList);
        Assert.Equal(expectedMarker, cut.Find(".edge-line").GetAttribute("marker-end"));
    }

    [Fact]
    public void Should_KeepTheSelectedArrow_When_ASelectedLinksSceneHasAProblem()
    {
        // Act
        var cut = RenderEdge(CanvasEdges.Choice("Go"), isSelected: true, severity: ValidationSeverity.Error);

        // Assert — selection keeps the line; the mark in the label layer keeps the severity (§13.3).
        Assert.Contains("edge-error", cut.Find(".canvas-edge").ClassList);
        Assert.Equal("url(#canvas-arrow-selected)", cut.Find(".edge-line").GetAttribute("marker-end"));
    }

    [Fact]
    public void Should_TakeNoSeverity_When_TheReportBlamesNothing()
    {
        // Act
        var cut = RenderEdge(CanvasEdges.Choice("Go"));

        // Assert
        var group = cut.Find(".canvas-edge");
        Assert.DoesNotContain("edge-error", group.ClassList);
        Assert.DoesNotContain("edge-warning", group.ClassList);
    }

    private IRenderedComponent<SvgHost> RenderEdge(
        CanvasEdge edge, bool isSelected = false, ValidationSeverity? severity = null, Action? onSelected = null) =>
        Render<SvgHost>(host => host.AddChildContent<SceneEdge>(parameters => parameters
            .Add(p => p.Edge, edge)
            .Add(p => p.IsSelected, isSelected)
            .Add(p => p.Severity, severity)
            .Add(p => p.OnSelected, onSelected ?? (() => { }))));
}
