using Fourthwall.Application;
using Fourthwall.Domain;
using Graph1x;
using Graph1x.Algorithms;
using Graph1x.Edges;

namespace Fourthwall.Infrastructure;

/// <remarks>
/// Adapts a Graph1x <see cref="DirectedGraph{TVertex, TEdge}"/> to <see cref="IStoryGraph"/>.
/// Forward reachability is a breadth-first search from the origin; reverse reachability walks the
/// graph's transpose, built once on first use and reused (a story is validated by building the
/// graph once and querying it several times). No Graph1x type crosses this boundary — every
/// method translates scene identifiers in and a plain <see cref="IReadOnlySet{T}"/> out.
/// </remarks>
internal sealed class Graph1xStoryGraph : IStoryGraph
{
    private readonly DirectedGraph<SceneId, Edge<SceneId>> _graph;
    private IDirectedGraph<SceneId, Edge<SceneId>>? _transpose;

    public Graph1xStoryGraph(DirectedGraph<SceneId, Edge<SceneId>> graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public IReadOnlySet<SceneId> ReachableFrom(SceneId origin) =>
        _graph.BreadthFirstSearch(origin).ToHashSet();

    public IReadOnlySet<SceneId> ScenesThatCanReachAny(IReadOnlySet<SceneId> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        // The transpose is only ever needed for this query and is skipped entirely when there is
        // nothing to walk back from — the common case of a story that has no endings yet.
        if (targets.Count == 0)
        {
            return new HashSet<SceneId>();
        }

        var transpose = _transpose ??= _graph.Transpose();

        var canReach = new HashSet<SceneId>();
        foreach (var target in targets)
        {
            canReach.UnionWith(transpose.BreadthFirstSearch(target));
        }

        return canReach;
    }
}
