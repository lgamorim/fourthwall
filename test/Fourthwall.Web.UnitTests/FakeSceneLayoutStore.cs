using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// An in-memory <see cref="ISceneLayoutStore"/> keeping the real store's per-scene upsert, so
/// saving one node's position leaves the rest of the layout alone.
/// </summary>
public sealed class FakeSceneLayoutStore : ISceneLayoutStore
{
    private readonly Dictionary<SceneId, ScenePosition> _positions = [];

    public int SaveCount { get; private set; }

    /// <summary>
    /// When set, the next save throws this instead of succeeding, and the failure is cleared.
    /// </summary>
    public Exception? FailNextSave { get; set; }

    public Task<IReadOnlyDictionary<SceneId, ScenePosition>> LoadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<SceneId, ScenePosition>>(
            new Dictionary<SceneId, ScenePosition>(_positions));

    public Task SaveAsync(
        IReadOnlyDictionary<SceneId, ScenePosition> positions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positions);

        if (FailNextSave is not null)
        {
            var failure = FailNextSave;
            FailNextSave = null;
            throw failure;
        }

        foreach (var (sceneId, position) in positions)
        {
            _positions[sceneId] = position;
        }

        SaveCount++;
        return Task.CompletedTask;
    }
}
