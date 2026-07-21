using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

/// <remarks>
/// Stands in for the Graph1x adapter that arrives in M4, so the validator's rules can be proven
/// without a graph library. Deliberately the simplest correct traversal.
/// </remarks>
internal sealed class InMemoryStoryGraph : IStoryGraph
{
    private readonly Dictionary<SceneId, List<SceneId>> _outgoing = [];
    private readonly Dictionary<SceneId, List<SceneId>> _incoming = [];

    public InMemoryStoryGraph(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        foreach (var scene in story.Scenes)
        {
            _outgoing.TryAdd(scene.Id, []);
            _incoming.TryAdd(scene.Id, []);
        }

        foreach (var scene in story.Scenes)
        {
            foreach (var target in scene.OutgoingSceneIds)
            {
                _outgoing[scene.Id].Add(target);
                _incoming[target].Add(scene.Id);
            }
        }
    }

    public IReadOnlySet<SceneId> ReachableFrom(SceneId origin) => Walk([origin], _outgoing);

    public IReadOnlySet<SceneId> ScenesThatCanReachAny(IReadOnlySet<SceneId> targets) =>
        Walk(targets, _incoming);

    private static HashSet<SceneId> Walk(
        IEnumerable<SceneId> seeds,
        Dictionary<SceneId, List<SceneId>> adjacency)
    {
        var visited = new HashSet<SceneId>();
        var pending = new Queue<SceneId>();

        foreach (var seed in seeds)
        {
            if (visited.Add(seed))
            {
                pending.Enqueue(seed);
            }
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();

            if (!adjacency.TryGetValue(current, out var neighbours))
            {
                continue;
            }

            foreach (var neighbour in neighbours)
            {
                if (visited.Add(neighbour))
                {
                    pending.Enqueue(neighbour);
                }
            }
        }

        return visited;
    }
}
