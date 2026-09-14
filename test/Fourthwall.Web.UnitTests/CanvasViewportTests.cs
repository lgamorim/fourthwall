using Fourthwall.Application;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class CanvasViewportTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Should_StartAtActualSizeAtTheOrigin_When_Created()
    {
        // Arrange & Act
        var viewport = new CanvasViewport();

        // Assert — the frame M20 drew: the page's origin at the window's top-left corner, 1:1.
        Assert.Equal("translate(0 0) scale(1)", viewport.Transform);
        Assert.False(viewport.IsMeasured);
    }

    [Fact]
    public void Should_KeepTheWorldPointUnderTheCursor_When_Zooming()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.PanBy(100, 50);
        var before = viewport.ToWorld(300, 200);

        // Act
        viewport.ZoomAt(300, 200, steps: 1);

        // Assert
        var after = viewport.ToWorld(300, 200);
        Assert.Equal(CanvasViewport.ZoomStep, viewport.Scale, Tolerance);
        Assert.Equal(before.X, after.X, Tolerance);
        Assert.Equal(before.Y, after.Y, Tolerance);
    }

    [Fact]
    public void Should_ClampTheScale_When_ZoomingInPastTheLimit()
    {
        // Arrange
        var viewport = new CanvasViewport();
        var before = viewport.ToWorld(120, 80);

        // Act
        viewport.ZoomAt(120, 80, steps: 100);

        // Assert — the limit holds, and the point under the cursor still does.
        Assert.Equal(CanvasViewport.MaxScale, viewport.Scale);
        var after = viewport.ToWorld(120, 80);
        Assert.Equal(before.X, after.X, Tolerance);
        Assert.Equal(before.Y, after.Y, Tolerance);
    }

    [Fact]
    public void Should_ClampTheScale_When_ZoomingOutPastTheLimit()
    {
        // Arrange
        var viewport = new CanvasViewport();

        // Act
        viewport.ZoomAt(0, 0, steps: -100);

        // Assert
        Assert.Equal(CanvasViewport.MinScale, viewport.Scale);
    }

    [Fact]
    public void Should_RoundTrip_When_ConvertingBetweenScreenAndWorld()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.ZoomAt(50, 20, steps: -3);
        viewport.PanBy(-12.5, 7);

        // Act
        var world = viewport.ToWorld(333, 111);
        var (screenX, screenY) = viewport.ToScreen(world);

        // Assert
        Assert.Equal(333, screenX, Tolerance);
        Assert.Equal(111, screenY, Tolerance);
    }

    [Fact]
    public void Should_MoveTheOriginByTheScreenDelta_When_Panned()
    {
        // Arrange — a pan is measured in window pixels whatever the zoom, so the map follows the
        // pointer exactly.
        var viewport = new CanvasViewport();
        viewport.ZoomAt(0, 0, steps: 1);

        // Act
        viewport.PanBy(30, -10);

        // Assert
        Assert.Equal(30, viewport.TranslateX);
        Assert.Equal(-10, viewport.TranslateY);
        var origin = viewport.ToWorld(0, 0);
        Assert.Equal(-30 / CanvasViewport.ZoomStep, origin.X, Tolerance);
        Assert.Equal(10 / CanvasViewport.ZoomStep, origin.Y, Tolerance);
    }

    [Fact]
    public void Should_ScaleAndCentreTheContent_When_Fitting()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);

        // Act
        viewport.Fit(new CanvasBounds(0, 0, 1600, 600), padding: 40);

        // Assert — the wider side sets the scale, and the shorter side is centred.
        Assert.Equal(0.45, viewport.Scale, Tolerance);
        Assert.Equal(40, viewport.TranslateX, Tolerance);
        Assert.Equal(165, viewport.TranslateY, Tolerance);
    }

    [Fact]
    public void Should_NotEnlargePastActualSize_When_FittingSmallContent()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);

        // Act
        viewport.Fit(new CanvasBounds(40, 40, 240, 104), padding: 40);

        // Assert — a small story is centred at 1:1, never blown up.
        Assert.Equal(1, viewport.Scale);
        Assert.Equal(260, viewport.TranslateX, Tolerance);
        Assert.Equal(228, viewport.TranslateY, Tolerance);
    }

    [Fact]
    public void Should_GoBelowTheWheelsFloor_When_FittingAVeryLargeStory()
    {
        // Arrange — "Show whole story" means the whole story, however large; the quarter is the
        // wheel's floor, not the frame's.
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);

        // Act
        viewport.Fit(new CanvasBounds(0, 0, 16000, 600), padding: 40);

        // Assert
        Assert.Equal(0.045, viewport.Scale, Tolerance);
        Assert.Equal(40, viewport.TranslateX, Tolerance);
    }

    [Fact]
    public void Should_NotZoomOutFurther_When_AlreadyBelowTheFloor()
    {
        // Arrange — a wheel notch out from a frame under the floor must not jump the map back in.
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);
        viewport.Fit(new CanvasBounds(0, 0, 16000, 600), padding: 40);

        // Act
        viewport.ZoomAt(400, 300, steps: -1);

        // Assert
        Assert.Equal(0.045, viewport.Scale, Tolerance);
    }

    [Fact]
    public void Should_ZoomIn_When_BelowTheFloor()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);
        viewport.Fit(new CanvasBounds(0, 0, 16000, 600), padding: 40);

        // Act
        viewport.ZoomAt(400, 300, steps: 1);

        // Assert
        Assert.Equal(0.045 * CanvasViewport.ZoomStep, viewport.Scale, Tolerance);
    }

    [Fact]
    public void Should_KeepTheView_When_FittingBeforeTheWindowIsMeasured()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.PanBy(10, 10);

        // Act
        viewport.Fit(new CanvasBounds(0, 0, 1600, 600), padding: 40);

        // Assert
        Assert.Equal("translate(10 10) scale(1)", viewport.Transform);
    }

    [Fact]
    public void Should_ReturnToActualSize_When_FittingEmptyBounds()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);
        viewport.ZoomAt(100, 100, steps: 2);

        // Act
        viewport.Fit(CanvasBounds.Empty, padding: 40);

        // Assert
        Assert.Equal("translate(0 0) scale(1)", viewport.Transform);
    }

    [Fact]
    public void Should_RestoreActualSizeAtTheOrigin_When_Reset()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.ZoomAt(100, 100, steps: 2);
        viewport.PanBy(-40, 25);

        // Act
        viewport.Reset();

        // Assert
        Assert.Equal("translate(0 0) scale(1)", viewport.Transform);
    }

    [Fact]
    public void Should_FormatTheTransformInvariantly_When_CurrentCultureUsesCommaDecimals()
    {
        // Arrange — pt-PT writes "10,5"; inside a transform attribute that splits the coordinate.
        using var _ = new CulturePin("pt-PT");
        var viewport = new CanvasViewport();
        viewport.ZoomAt(0, 0, steps: 1);
        viewport.PanBy(10.5, 0);

        // Act
        var transform = viewport.Transform;

        // Assert
        Assert.Equal("translate(10.5 0) scale(1.2)", transform);
    }

    [Fact]
    public void Should_PanToCentreThePoint_When_CentringOnIt()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);
        viewport.ZoomAt(0, 0, steps: 1);

        // Act
        viewport.CentreOn(new ScenePosition(1000, -250));

        // Assert — the scale is kept; only the map slides.
        var (screenX, screenY) = viewport.ToScreen(new ScenePosition(1000, -250));
        Assert.Equal(400, screenX, Tolerance);
        Assert.Equal(300, screenY, Tolerance);
        Assert.Equal(CanvasViewport.ZoomStep, viewport.Scale, Tolerance);
    }

    [Fact]
    public void Should_ZoomAboutTheWindowsCentre_When_ZoomingWithoutAnAnchor()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);
        viewport.PanBy(-200, 90);
        var before = viewport.ToWorld(400, 300);

        // Act
        viewport.ZoomBy(steps: -1);

        // Assert
        var after = viewport.ToWorld(400, 300);
        Assert.Equal(before.X, after.X, Tolerance);
        Assert.Equal(before.Y, after.Y, Tolerance);
        Assert.Equal(1 / CanvasViewport.ZoomStep, viewport.Scale, Tolerance);
    }

    [Fact]
    public void Should_ReportTheBoundsAsShown_When_TheyLieWhollyInsideTheWindow()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);

        // Act & Assert
        Assert.True(viewport.Shows(new CanvasBounds(40, 40, 760, 560)));
        Assert.False(viewport.Shows(new CanvasBounds(40, 40, 801, 560)));
        Assert.False(viewport.Shows(new CanvasBounds(-1, 40, 760, 560)));
    }

    [Fact]
    public void Should_ReportTheBoundsAsTouched_When_AnyPartLiesInsideTheWindow()
    {
        // Arrange
        var viewport = new CanvasViewport();
        viewport.SetSize(800, 600);

        // Act & Assert — a page half off the edge is still in view; one past the edge is not.
        Assert.True(viewport.Touches(new CanvasBounds(-100, 40, 100, 104)));
        Assert.True(viewport.Touches(new CanvasBounds(700, 550, 900, 614)));
        Assert.False(viewport.Touches(new CanvasBounds(-300, 40, -100, 104)));
        Assert.False(viewport.Touches(new CanvasBounds(40, 600, 240, 664)));
    }

    [Fact]
    public void Should_ReportNothingAsTouched_When_TheWindowIsNotMeasured()
    {
        // Arrange
        var viewport = new CanvasViewport();

        // Act & Assert
        Assert.False(viewport.Touches(new CanvasBounds(0, 0, 10, 10)));
    }

    [Fact]
    public void Should_ReportNothingAsShown_When_TheWindowIsNotMeasured()
    {
        // Arrange
        var viewport = new CanvasViewport();

        // Act & Assert
        Assert.False(viewport.Shows(new CanvasBounds(0, 0, 10, 10)));
    }

    [Theory]
    [InlineData(-1, 600)]
    [InlineData(800, -1)]
    public void Should_Throw_When_MeasuredWithANegativeSize(double width, double height)
    {
        // Arrange
        var viewport = new CanvasViewport();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => viewport.SetSize(width, height));
    }
}
