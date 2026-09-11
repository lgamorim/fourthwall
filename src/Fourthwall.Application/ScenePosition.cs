namespace Fourthwall.Application;

/// <summary>
/// Where a scene's node sits on the editor's canvas.
/// </summary>
/// <param name="X">The horizontal coordinate, in canvas units.</param>
/// <param name="Y">The vertical coordinate, in canvas units.</param>
/// <remarks>
/// This is editor state, not story state: a position says nothing about what the story means, and
/// the game runtime ignores it (design doc decision D7). Coordinates are unbounded and may be
/// negative — the canvas has no origin corner, so a node dragged up and left of every other one is
/// perfectly ordinary.
/// </remarks>
public readonly record struct ScenePosition(double X, double Y);
