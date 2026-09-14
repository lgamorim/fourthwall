using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class CanvasInteractionTests
{
    private const double Tolerance = 1e-9;

    private static readonly SceneId Scene = SceneId.New();
    private static readonly ScenePosition NodeAt = new(40, 40);

    private readonly CanvasViewport _viewport = new();

    [Fact]
    public void Should_StartIdle_When_Created()
    {
        // Arrange & Act
        var interaction = new CanvasInteraction(_viewport);

        // Assert
        Assert.Equal(CanvasInteractionMode.Idle, interaction.Mode);
        Assert.False(interaction.LastGestureWasDrag);
    }

    [Fact]
    public void Should_PanTheViewport_When_TheGroundIsDragged()
    {
        // Arrange — the map follows the pointer in window pixels, whatever the zoom.
        _viewport.ZoomAt(0, 0, steps: 1);
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(scene: null, nodePosition: null, 100, 100);

        // Act
        var first = interaction.PointerMove(130, 90);
        var second = interaction.PointerMove(135, 95);

        // Assert
        Assert.Equal(CanvasInteractionMode.Panning, interaction.Mode);
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(35, _viewport.TranslateX);
        Assert.Equal(-5, _viewport.TranslateY);
    }

    [Fact]
    public void Should_MoveTheNode_When_ItIsDraggedPastTheThreshold()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);

        // Act
        var move = interaction.PointerMove(110, 103);

        // Assert
        Assert.Equal(CanvasInteractionMode.DraggingNode, interaction.Mode);
        Assert.Equal(new NodeMove(Scene, new ScenePosition(50, 43)), move);
    }

    [Fact]
    public void Should_NotMoveTheNode_When_ThePointerStaysWithinTheThreshold()
    {
        // Arrange — a press with a little jitter is a click, not a move.
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);

        // Act
        var move = interaction.PointerMove(102, 103);

        // Assert
        Assert.Null(move);
        Assert.Equal(CanvasInteractionMode.Pressing, interaction.Mode);
    }

    [Fact]
    public void Should_KeepDragging_When_ThePointerReturnsWithinTheThreshold()
    {
        // Arrange — once a drag has begun, bringing the node back near where it started still moves it.
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);
        interaction.PointerMove(120, 100);

        // Act
        var move = interaction.PointerMove(101, 100);

        // Assert
        Assert.Equal(new NodeMove(Scene, new ScenePosition(41, 40)), move);
        Assert.Equal(CanvasInteractionMode.DraggingNode, interaction.Mode);
    }

    [Fact]
    public void Should_ReportTheDrag_When_ThePointerIsReleasedAfterADrag()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);
        interaction.PointerMove(110, 103);
        interaction.PointerMove(130, 100);

        // Act
        var result = interaction.PointerUp();

        // Assert — the final position, and the mark the click that follows a drag must not select.
        Assert.Equal(new NodeMove(Scene, new ScenePosition(70, 40)), result);
        Assert.Equal(CanvasInteractionMode.Idle, interaction.Mode);
        Assert.True(interaction.LastGestureWasDrag);
    }

    [Fact]
    public void Should_ReportNoDrag_When_ThePointerIsReleasedAfterAClick()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);
        interaction.PointerMove(101, 101);

        // Act
        var result = interaction.PointerUp();

        // Assert
        Assert.Null(result);
        Assert.Equal(CanvasInteractionMode.Idle, interaction.Mode);
        Assert.False(interaction.LastGestureWasDrag);
    }

    [Fact]
    public void Should_ReportNoDrag_When_ThePointerIsReleasedAfterAPan()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(scene: null, nodePosition: null, 100, 100);
        interaction.PointerMove(200, 200);

        // Act
        var result = interaction.PointerUp();

        // Assert
        Assert.Null(result);
        Assert.Equal(CanvasInteractionMode.Idle, interaction.Mode);
        Assert.False(interaction.LastGestureWasDrag);
    }

    [Fact]
    public void Should_IgnoreMoves_When_Idle()
    {
        // Arrange — the shim forwards every captured move; those after a release mean nothing.
        var interaction = new CanvasInteraction(_viewport);

        // Act
        var move = interaction.PointerMove(500, 500);

        // Assert
        Assert.Null(move);
        Assert.Equal("translate(0 0) scale(1)", _viewport.Transform);
    }

    [Fact]
    public void Should_ScaleTheDragDelta_When_Zoomed()
    {
        // Arrange — twelve window pixels are ten canvas units at ×1.2, so the node stays under the pointer.
        _viewport.ZoomAt(0, 0, steps: 1);
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);

        // Act
        var move = interaction.PointerMove(112, 100);

        // Assert
        Assert.NotNull(move);
        Assert.Equal(50, move.Position.X, Tolerance);
        Assert.Equal(40, move.Position.Y, Tolerance);
    }

    [Fact]
    public void Should_NotPan_When_ANodeIsPressed()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);

        // Act
        interaction.PointerMove(102, 102);
        interaction.PointerMove(150, 150);

        // Assert
        Assert.Equal("translate(0 0) scale(1)", _viewport.Transform);
    }

    [Fact]
    public void Should_ForgetTheDrag_When_ThePointerIsPressedAgain()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);
        interaction.PointerMove(130, 100);
        interaction.PointerUp();

        // Act
        interaction.PointerDown(Scene, NodeAt, 100, 100);

        // Assert
        Assert.False(interaction.LastGestureWasDrag);
    }

    [Fact]
    public void Should_IgnoreASecondPress_When_AGestureIsInProgress()
    {
        // Arrange — a second finger, or a press the browser delivers before the release.
        var interaction = new CanvasInteraction(_viewport);
        interaction.PointerDown(Scene, NodeAt, 100, 100);
        interaction.PointerMove(130, 100);

        // Act
        interaction.PointerDown(scene: null, nodePosition: null, 500, 500);
        var move = interaction.PointerMove(140, 100);

        // Assert
        Assert.Equal(CanvasInteractionMode.DraggingNode, interaction.Mode);
        Assert.Equal(new NodeMove(Scene, new ScenePosition(80, 40)), move);
    }

    [Fact]
    public void Should_Throw_When_ANodeIsPressedWithoutItsPosition()
    {
        // Arrange
        var interaction = new CanvasInteraction(_viewport);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => interaction.PointerDown(Scene, nodePosition: null, 100, 100));
    }

    [Fact]
    public void Should_Throw_When_ViewportIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CanvasInteraction(null!));
    }
}
