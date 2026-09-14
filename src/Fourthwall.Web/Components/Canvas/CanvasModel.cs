using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// The nodes and edges the canvas renders for a story, built once per layout from the story's
/// scenes and their positions.
/// </summary>
public sealed class CanvasModel
{
    private CanvasModel(IReadOnlyList<CanvasNode> nodes, IReadOnlyList<CanvasEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;

        if (nodes.Count > 0)
        {
            var loops = edges.Where(edge => edge.IsSelfLoop).ToList();
            var loopReach = loops
                .Select(loop => nodes.First(node => node.Scene.Id == loop.Source).Position.X
                    + CanvasGeometry.NodeWidth + CanvasGeometry.SelfLoopReach(loop.ParallelIndex))
                .DefaultIfEmpty(double.NegativeInfinity)
                .Max();
            var loopTop = loops
                .Select(loop => nodes.First(node => node.Scene.Id == loop.Source).Position.Y
                    - CanvasGeometry.SelfLoopRise(loop.ParallelIndex))
                .DefaultIfEmpty(double.PositiveInfinity)
                .Min();

            Bounds = new CanvasBounds(
                nodes.Min(node => node.Position.X),
                Math.Min(nodes.Min(node => node.Position.Y), loopTop),
                Math.Max(nodes.Max(node => node.Position.X + CanvasGeometry.NodeWidth), loopReach),
                nodes.Max(node => node.Position.Y + CanvasGeometry.NodeHeight));
        }
    }

    /// <summary>
    /// Gets the edges the drawing reaches, tight to its nodes and their self-loops; empty with no
    /// nodes. The viewport adds its own margin when it frames the content.
    /// </summary>
    public CanvasBounds Bounds { get; }

    /// <summary>
    /// Gets every node, in paint order — later nodes sit on top of earlier ones.
    /// </summary>
    public IReadOnlyList<CanvasNode> Nodes { get; }

    /// <summary>
    /// Gets every edge, one per choice and one per follow-up.
    /// </summary>
    public IReadOnlyList<CanvasEdge> Edges { get; }

    /// <summary>
    /// Builds the canvas model for a story at the given node positions.
    /// </summary>
    /// <param name="story">The story to render.</param>
    /// <param name="positions">Every scene's position; must have an entry for each of the story's scenes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="story"/> or <paramref name="positions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="positions"/> has no entry for one of <paramref name="story"/>'s scenes, or for
    /// the target of one of its transitions.
    /// </exception>
    public static CanvasModel Build(Story story, IReadOnlyDictionary<SceneId, ScenePosition> positions)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(positions);

        var orderedScenes = Scenes.Ordered(story).ToList();
        var nodes = orderedScenes
            .Select(scene => new CanvasNode(
                scene, RequirePosition(positions, scene.Id), scene.Id == story.StartSceneId))
            .ToList();

        var parallelCounts = new Dictionary<(SceneId Source, SceneId Target), int>();
        var edges = new List<CanvasEdge>();

        foreach (var scene in orderedScenes)
        {
            for (var index = 0; index < scene.Choices.Count; index++)
            {
                var choice = scene.Choices[index];
                edges.Add(BuildEdge(
                    new CanvasEdgeKey(scene.Id, index), scene.Id, choice.TargetSceneId, choice.Label,
                    positions, parallelCounts));
            }

            if (scene.FollowUpSceneId is { } followUp)
            {
                edges.Add(BuildEdge(
                    new CanvasEdgeKey(scene.Id, null), scene.Id, followUp, string.Empty,
                    positions, parallelCounts));
            }
        }

        return new CanvasModel(nodes, edges);
    }

    /// <summary>
    /// Finds the topmost node whose bounding box contains a world-space point.
    /// </summary>
    /// <param name="world">The point to test, in the same coordinate space as <see cref="CanvasNode.Position"/>.</param>
    /// <returns>The topmost node's scene, or <see langword="null"/> when no node contains the point.</returns>
    /// <remarks>
    /// Hit-testing runs in C# rather than relying on DOM hover: pointer capture during a drag
    /// (M21/M22) suppresses <c>pointerover</c> on everything but the captured element.
    /// </remarks>
    public SceneId? HitTest(ScenePosition world)
    {
        for (var index = Nodes.Count - 1; index >= 0; index--)
        {
            var node = Nodes[index];
            var left = node.Position.X;
            var top = node.Position.Y;

            if (world.X >= left && world.X <= left + CanvasGeometry.NodeWidth &&
                world.Y >= top && world.Y <= top + CanvasGeometry.NodeHeight)
            {
                return node.Scene.Id;
            }
        }

        return null;
    }

    private static CanvasEdge BuildEdge(
        CanvasEdgeKey key,
        SceneId source,
        SceneId target,
        string label,
        IReadOnlyDictionary<SceneId, ScenePosition> positions,
        Dictionary<(SceneId Source, SceneId Target), int> parallelCounts)
    {
        var pair = (Source: source, Target: target);
        var parallelIndex = parallelCounts.GetValueOrDefault(pair);
        parallelCounts[pair] = parallelIndex + 1;

        var isSelfLoop = source == target;
        var fromPosition = RequirePosition(positions, source);
        var toPosition = RequirePosition(positions, target);

        var pathData = isSelfLoop
            ? CanvasGeometry.SelfLoopPath(fromPosition, parallelIndex)
            : CanvasGeometry.EdgePath(fromPosition, toPosition, parallelIndex);

        var (labelX, labelY) = isSelfLoop
            ? CanvasGeometry.SelfLoopLabelPoint(fromPosition, parallelIndex)
            : CanvasGeometry.LabelPoint(fromPosition, toPosition, parallelIndex);

        return new CanvasEdge(key, source, target, label, parallelIndex, isSelfLoop, pathData, labelX, labelY);
    }

    private static ScenePosition RequirePosition(
        IReadOnlyDictionary<SceneId, ScenePosition> positions, SceneId sceneId)
    {
        if (!positions.TryGetValue(sceneId, out var position))
        {
            throw new ArgumentException($"No position was given for scene '{sceneId.Value}'.", nameof(positions));
        }

        return position;
    }
}
