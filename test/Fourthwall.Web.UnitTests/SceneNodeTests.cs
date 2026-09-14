using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;
using Fourthwall.Web.Composition;

using Microsoft.AspNetCore.Components.Web;

namespace Fourthwall.Web.UnitTests;

public class SceneNodeTests : BunitContext
{
    [Theory]
    [InlineData(SceneKind.Choice, "node-kind-choice")]
    [InlineData(SceneKind.Linear, "node-kind-linear")]
    [InlineData(SceneKind.Ending, "node-kind-ending")]
    public void Should_MarkTheKind_When_Rendered(SceneKind kind, string expectedClass)
    {
        // Arrange
        var node = Node(kind, "A storm gathers");

        // Act
        var cut = RenderNode(node);

        // Assert — the class picks the silhouette; the outline and the caption word carry it too.
        var group = cut.Find(".canvas-node");
        Assert.Contains(expectedClass, group.ClassList);
        Assert.Equal(CanvasGeometry.NodeOutline(kind), cut.Find(".node-shape").GetAttribute("d"));
        Assert.Equal(kind.ToString(), cut.Find(".node-caption").TextContent);
    }

    [Fact]
    public void Should_PlaceTheNode_When_Rendered()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers") with { Position = new ScenePosition(40.5, 144) };

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Equal("translate(40.5 144)", cut.Find(".canvas-node").GetAttribute("transform"));
    }

    [Fact]
    public void Should_IdentifyTheScene_When_Rendered()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Equal(node.Scene.Id.Value.ToString(), cut.Find(".canvas-node").GetAttribute("data-scene-id"));
    }

    [Fact]
    public void Should_ShowTheStartTag_When_TheSceneStartsTheStory()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers") with { IsStart = true };

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Equal("Start", cut.Find(".node-start").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowNoStartTag_When_TheSceneDoesNotStartTheStory()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Empty(cut.FindAll(".node-start"));
    }

    [Fact]
    public void Should_ShowTheThumbnail_When_TheSceneHasAnImage()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers");
        node.Scene.AttachImage("assets/storm.png");

        // Act
        var cut = RenderNode(node);

        // Assert — served from the open story's folder, clipped to the shared plate.
        var image = cut.Find(".node-thumbnail image");
        Assert.Equal(StoryAssetEndpoint.UrlPrefix + "assets/storm.png", image.GetAttribute("href"));
        Assert.Equal("url(#canvas-node-image)", image.GetAttribute("clip-path"));
    }

    [Fact]
    public void Should_ShowNoThumbnail_When_TheSceneHasNoImage()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Empty(cut.FindAll(".node-thumbnail"));
    }

    [Fact]
    public void Should_CutTheLabelAndKeepTheWholeTextInTheTitle_When_TheTextIsLong()
    {
        // Arrange
        const string text = "A storm gathers over the Marianne. The mast groans.";
        var node = Node(SceneKind.Linear, text);

        // Act
        var cut = RenderNode(node);

        // Assert — SVG text does not wrap, so the label is short and the title has the rest.
        Assert.Equal("A storm gathers over t…", cut.Find(".node-label").TextContent);
        Assert.Equal(text, cut.Find(".canvas-node > title").TextContent);
    }

    [Fact]
    public void Should_CutTheLabelShorter_When_AThumbnailTakesItsRoom()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers over the Marianne.");
        node.Scene.AttachImage("assets/storm.png");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Equal("A storm gathers…", cut.Find(".node-label").TextContent);
    }

    [Fact]
    public void Should_HangTheRibbon_When_Selected()
    {
        // Arrange
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node, isSelected: true);

        // Assert
        Assert.Contains("node-selected", cut.Find(".canvas-node").ClassList);
        Assert.Equal(CanvasGeometry.RibbonPath, cut.Find(".node-ribbon").GetAttribute("d"));
        Assert.Equal("true", cut.Find(".canvas-node").GetAttribute("aria-current"));
    }

    [Fact]
    public void Should_HangNoRibbon_When_NotSelected()
    {
        // Arrange
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.DoesNotContain("node-selected", cut.Find(".canvas-node").ClassList);
        Assert.Empty(cut.FindAll(".node-ribbon"));
        Assert.Null(cut.Find(".canvas-node").GetAttribute("aria-current"));
    }

    [Fact]
    public void Should_BeReachableByKeyboard_When_Rendered()
    {
        // Arrange
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node);

        // Assert
        var group = cut.Find(".canvas-node");
        Assert.Equal("0", group.GetAttribute("tabindex"));
        Assert.Equal("button", group.GetAttribute("role"));
        Assert.Equal("A fork, Choice", group.GetAttribute("aria-label"));
    }

    [Fact]
    public void Should_RaiseSelection_When_Clicked()
    {
        // Arrange
        var selected = 0;
        var cut = RenderNode(Node(SceneKind.Choice, "A fork"), onSelected: () => selected++);

        // Act
        cut.Find(".canvas-node").Click();

        // Assert
        Assert.Equal(1, selected);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Should_RaiseSelection_When_EnterOrSpaceIsPressed(string key)
    {
        // Arrange
        var selected = 0;
        var cut = RenderNode(Node(SceneKind.Choice, "A fork"), onSelected: () => selected++);

        // Act
        cut.Find(".canvas-node").KeyDown(new KeyboardEventArgs { Key = key });

        // Assert
        Assert.Equal(1, selected);
    }

    [Fact]
    public void Should_NotRaiseSelection_When_AnotherKeyIsPressed()
    {
        // Arrange
        var selected = 0;
        var cut = RenderNode(Node(SceneKind.Choice, "A fork"), onSelected: () => selected++);

        // Act
        cut.Find(".canvas-node").KeyDown(new KeyboardEventArgs { Key = "Tab" });

        // Assert
        Assert.Equal(0, selected);
    }

    [Fact]
    public void Should_RaisePointerDown_When_Pressed()
    {
        // Arrange — the canvas needs to know which node is pressed and where, to begin a drag.
        PointerEventArgs? pressed = null;
        var cut = RenderNode(Node(SceneKind.Choice, "A fork"), onPointerDown: args => pressed = args);

        // Act
        cut.Find(".canvas-node").PointerDown(new PointerEventArgs { Button = 0, ClientX = 12, ClientY = 34 });

        // Assert
        Assert.NotNull(pressed);
        Assert.Equal(12, pressed.ClientX);
        Assert.Equal(34, pressed.ClientY);
    }

    [Fact]
    public void Should_MarkTheNode_When_ItIsBeingDragged()
    {
        // Arrange
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node, isDragging: true);

        // Assert
        Assert.Contains("node-dragging", cut.Find(".canvas-node").ClassList);
    }

    [Fact]
    public void Should_NotMarkTheNode_When_ItIsStill()
    {
        // Arrange
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.DoesNotContain("node-dragging", cut.Find(".canvas-node").ClassList);
    }

    private static CanvasNode Node(SceneKind kind, string text)
    {
        var outcome = kind == SceneKind.Ending ? EndingOutcome.Victory() : null;
        return new CanvasNode(new Scene(SceneId.New(), kind, text, outcome), new ScenePosition(0, 0), IsStart: false);
    }

    private IRenderedComponent<SvgHost> RenderNode(
        CanvasNode node,
        bool isSelected = false,
        bool isDragging = false,
        Action? onSelected = null,
        Action<PointerEventArgs>? onPointerDown = null) =>
        Render<SvgHost>(host => host.AddChildContent<SceneNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, isSelected)
            .Add(p => p.IsDragging, isDragging)
            .Add(p => p.OnSelected, onSelected ?? (() => { }))
            .Add(p => p.OnPointerDown, onPointerDown ?? (_ => { }))));
}
