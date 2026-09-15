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
    public void Should_RaiseTheClick_When_Clicked()
    {
        // Arrange — the browser clicks a node after a drag of it too, so a click goes to the canvas
        // to decide whether it selects; the keys a button answers select outright.
        var clicked = 0;
        var selected = 0;
        var cut = RenderNode(
            Node(SceneKind.Choice, "A fork"), onSelected: () => selected++, onClicked: () => clicked++);

        // Act
        cut.Find(".canvas-node").Click();

        // Assert
        Assert.Equal(1, clicked);
        Assert.Equal(0, selected);
    }

    [Theory]
    [InlineData(SceneKind.Choice)]
    [InlineData(SceneKind.Linear)]
    public void Should_ShowAPortOnTheRightEdge_When_TheSceneCanLeadSomewhere(SceneKind kind)
    {
        // Arrange
        var node = Node(kind, "A fork");

        // Act
        var cut = RenderNode(node);

        // Assert — at the right centre, where every link leaves.
        var mark = cut.Find(".node-port .node-port-mark");
        Assert.Equal(CanvasGeometry.Invariant(CanvasGeometry.NodeWidth), mark.GetAttribute("cx"));
        Assert.Equal(CanvasGeometry.Invariant(CanvasGeometry.NodeHeight / 2), mark.GetAttribute("cy"));
        Assert.Equal(CanvasGeometry.Invariant(CanvasGeometry.PortRadius), mark.GetAttribute("r"));
        Assert.Equal("true", cut.Find(".node-port").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Should_ShowNoPort_When_TheSceneIsAnEnding()
    {
        // Arrange — nothing leaves a full stop.
        var node = Node(SceneKind.Ending, "You drown");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Empty(cut.FindAll(".node-port"));
    }

    [Fact]
    public void Should_RaiseThePortPressAndNotTheNodePress_When_ThePortIsPressed()
    {
        // Arrange
        PointerEventArgs? portPressed = null;
        var nodePressed = 0;
        var cut = RenderNode(
            Node(SceneKind.Choice, "A fork"),
            onPointerDown: _ => nodePressed++,
            onPortPointerDown: args => portPressed = args);

        // Act
        cut.Find(".node-port").PointerDown(new PointerEventArgs { Button = 0, ClientX = 12, ClientY = 34 });

        // Assert — drawing a link never starts moving the page.
        Assert.NotNull(portPressed);
        Assert.Equal(12, portPressed.ClientX);
        Assert.Equal(0, nodePressed);
    }

    [Fact]
    public void Should_NotRaiseTheClick_When_ThePortIsClicked()
    {
        // Arrange — the click after a drawn link lands on the port; the canvas selects the source
        // itself. bUnit only dispatches a click to an element with its own handler, and the port
        // has none — it only stops the click — so the directive is pinned in the markup instead.
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.Contains(
            cut.Find(".node-port").Attributes,
            attribute => attribute.Name.Equals("blazor:onclick:stopPropagation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_MarkTheNode_When_ItIsTheDropTarget()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "Below deck");

        // Act
        var cut = RenderNode(node, isDropTarget: true);

        // Assert
        Assert.Contains("node-drop-target", cut.Find(".canvas-node").ClassList);
    }

    [Fact]
    public void Should_MarkTheNode_When_ALinkIsDrawnFromIt()
    {
        // Arrange
        var node = Node(SceneKind.Choice, "A fork");

        // Act
        var cut = RenderNode(node, isDrawingSource: true);

        // Assert
        Assert.Contains("node-drawing-source", cut.Find(".canvas-node").ClassList);
        Assert.DoesNotContain("node-drop-target", cut.Find(".canvas-node").ClassList);
    }

    [Fact]
    public void Should_PromptTheCreator_When_TheSceneHasNoText()
    {
        // Arrange — a scene added on the map starts empty.
        var node = Node(SceneKind.Linear, string.Empty);

        // Act
        var cut = RenderNode(node);

        // Assert
        var label = cut.Find(".node-label");
        Assert.Equal("Write this scene", label.TextContent);
        Assert.Contains("node-label-prompt", label.ClassList);
    }

    [Fact]
    public void Should_NotPrompt_When_TheSceneHasText()
    {
        // Arrange
        var node = Node(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderNode(node);

        // Assert
        Assert.DoesNotContain("node-label-prompt", cut.Find(".node-label").ClassList);
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
    public void Should_RaiseFocus_When_Focused()
    {
        // Arrange — the canvas brings a scene into view when the keyboard lands on it off-screen.
        var focused = 0;
        var cut = RenderNode(Node(SceneKind.Choice, "A fork"), onFocused: () => focused++);

        // Act
        cut.Find(".canvas-node").Focus();

        // Assert
        Assert.Equal(1, focused);
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
        bool isDropTarget = false,
        bool isDrawingSource = false,
        Action? onSelected = null,
        Action? onClicked = null,
        Action<PointerEventArgs>? onPointerDown = null,
        Action<PointerEventArgs>? onPortPointerDown = null,
        Action? onFocused = null) =>
        Render<SvgHost>(host => host.AddChildContent<SceneNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, isSelected)
            .Add(p => p.IsDragging, isDragging)
            .Add(p => p.IsDropTarget, isDropTarget)
            .Add(p => p.IsDrawingSource, isDrawingSource)
            .Add(p => p.OnSelected, onSelected ?? (() => { }))
            .Add(p => p.OnClicked, onClicked ?? (() => { }))
            .Add(p => p.OnPointerDown, onPointerDown ?? (_ => { }))
            .Add(p => p.OnPortPointerDown, onPortPointerDown ?? (_ => { }))
            .Add(p => p.OnFocused, onFocused ?? (() => { }))));
}
