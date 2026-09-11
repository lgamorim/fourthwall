using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// A transition as it renders on the canvas, with its curve and label position already resolved
/// to pre-formatted SVG coordinates.
/// </summary>
/// <param name="Key">Which of the source scene's outgoing transitions this edge represents.</param>
/// <param name="Source">The scene the transition leads from.</param>
/// <param name="Target">The scene the transition leads to.</param>
/// <param name="Label">The choice's label, or empty for a follow-up.</param>
/// <param name="ParallelIndex">
/// This edge's position among the other edges that share the same source and target, used to
/// fan out edges that would otherwise overlap.
/// </param>
/// <param name="IsSelfLoop">Whether <see cref="Source"/> and <see cref="Target"/> are the same scene.</param>
/// <param name="PathData">The SVG path data for this edge's curve, formatted independently of the current culture.</param>
/// <param name="LabelX">The label's x-coordinate, formatted independently of the current culture.</param>
/// <param name="LabelY">The label's y-coordinate, formatted independently of the current culture.</param>
public sealed record CanvasEdge(
    CanvasEdgeKey Key,
    SceneId Source,
    SceneId Target,
    string Label,
    int ParallelIndex,
    bool IsSelfLoop,
    string PathData,
    string LabelX,
    string LabelY);
