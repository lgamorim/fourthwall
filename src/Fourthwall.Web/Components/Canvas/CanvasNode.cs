using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// A scene as it renders on the canvas: which scene it is, where it sits, and whether it is the
/// story's start scene.
/// </summary>
/// <param name="Scene">The scene this node represents.</param>
/// <param name="Position">Where the node's top-left corner sits, in canvas units.</param>
/// <param name="IsStart">Whether this scene is the story's start scene.</param>
public sealed record CanvasNode(Scene Scene, ScenePosition Position, bool IsStart)
{
    /// <summary>
    /// The SVG group transform that places this node at <see cref="Position"/>, formatted
    /// independently of the current culture.
    /// </summary>
    public string Transform =>
        $"translate({CanvasGeometry.Invariant(Position.X)} {CanvasGeometry.Invariant(Position.Y)})";
}
