using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// Builds canvas edges for the link component tests, which need an edge's shape, not a story.
/// </summary>
internal static class CanvasEdges
{
    public static CanvasEdge Choice(string label) => Build(choiceIndex: 0, label);

    public static CanvasEdge FollowUp() => Build(choiceIndex: null, string.Empty);

    private static CanvasEdge Build(int? choiceIndex, string label)
    {
        var source = SceneId.New();
        return new CanvasEdge(
            new CanvasEdgeKey(source, choiceIndex), source, SceneId.New(), label,
            ParallelIndex: 0, IsSelfLoop: false, PathData: "M 240,72 C 300,72 300,72 360,72",
            LabelX: "300", LabelY: "62");
    }
}
