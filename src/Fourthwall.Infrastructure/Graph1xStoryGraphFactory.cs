using Fourthwall.Application;
using Fourthwall.Domain;
using Graph1x;
using Graph1x.Edges;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Builds an <see cref="IStoryGraph"/> backed by Graph1x from a story.
/// </summary>
public sealed class Graph1xStoryGraphFactory : IStoryGraphFactory
{
    /// <inheritdoc />
    public IStoryGraph Create(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        var graph = new DirectedGraph<SceneId, Edge<SceneId>>();

        // Every scene is added as a vertex first — an isolated scene has no edges to imply it, and
        // the reachability queries throw if asked to walk from a scene that is not a vertex.
        foreach (var scene in story.Scenes)
        {
            graph.AddVertex(scene.Id);
        }

        // One edge per transition. DirectedGraph collapses parallel edges, which is harmless here:
        // IStoryGraph answers only reachability, and out-degree comes from the domain. A future
        // edge-count query would need DirectedMultigraph to avoid undercounting duplicates.
        foreach (var scene in story.Scenes)
        {
            foreach (var target in scene.OutgoingSceneIds)
            {
                graph.AddEdge(new Edge<SceneId>(scene.Id, target));
            }
        }

        return new Graph1xStoryGraph(graph);
    }
}
