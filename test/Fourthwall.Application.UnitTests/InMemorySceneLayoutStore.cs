using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

/// <remarks>
/// Stands in for the SQLite-backed layout store, so code written against
/// <see cref="ISceneLayoutStore"/> can be exercised without a database. It keeps the positions in a
/// dictionary, which gives the same per-scene upsert the real store provides: saving one entry
/// leaves the rest of the layout alone.
/// </remarks>
internal sealed class InMemorySceneLayoutStore : ISceneLayoutStore
{
    private readonly Dictionary<SceneId, ScenePosition> _positions = [];

    public Task<IReadOnlyDictionary<SceneId, ScenePosition>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyDictionary<SceneId, ScenePosition>>(
            new Dictionary<SceneId, ScenePosition>(_positions));
    }

    public Task SaveAsync(
        IReadOnlyDictionary<SceneId, ScenePosition> positions, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(positions);

        foreach (var (sceneId, position) in positions)
        {
            _positions[sceneId] = position;
        }

        return Task.CompletedTask;
    }
}
