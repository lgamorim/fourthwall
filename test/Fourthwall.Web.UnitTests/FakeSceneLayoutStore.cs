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

    public int LoadCount { get; private set; }

    /// <summary>
    /// When set, the next save throws this instead of succeeding, and the failure is cleared.
    /// </summary>
    public Exception? FailNextSave { get; set; }

    /// <summary>
    /// When set, the next load throws this instead of succeeding, and the failure is cleared.
    /// </summary>
    public Exception? FailNextLoad { get; set; }

    /// <summary>
    /// When set, loads wait on this before returning, so a test can show another story while a
    /// load is still in flight without depending on timing.
    /// </summary>
    public TaskCompletionSource? LoadGate { get; set; }

    public async Task<IReadOnlyDictionary<SceneId, ScenePosition>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (FailNextLoad is not null)
        {
            var failure = FailNextLoad;
            FailNextLoad = null;
            throw failure;
        }

        LoadCount++;
        var positions = new Dictionary<SceneId, ScenePosition>(_positions);

        if (LoadGate is not null)
        {
            await LoadGate.Task;
        }

        return positions;
    }

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
