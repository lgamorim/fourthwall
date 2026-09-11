using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Reads and writes where the editor's canvas places each scene of the open story.
/// </summary>
/// <remarks>
/// Node positions are editor-only state stored alongside the story, so they get their own port
/// rather than riding on <see cref="IStoryRepository"/>: dragging a node changes nothing about the
/// story, and routing it through a story save would rewrite the whole aggregate and tell every
/// component the story had changed.
/// <para>
/// A position belongs to a scene that has been saved. Scenes that lose their position do so by
/// being deleted — the store never has to prune, because the persisted layout is derived state
/// that follows its scene.
/// </para>
/// </remarks>
public interface ISceneLayoutStore
{
    /// <summary>
    /// Reads every stored node position.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The stored position of each placed scene, which may be empty — a story that has never been
    /// opened on the canvas has no positions at all, and a scene added since has none of its own.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task<IReadOnlyDictionary<SceneId, ScenePosition>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the given node positions, replacing the stored position of each scene named.
    /// </summary>
    /// <param name="positions">
    /// The positions to store, keyed by scene. Scenes absent from this dictionary keep the position
    /// they had: a single drag saves one entry without disturbing the rest of the layout.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the positions have been stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="positions"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A position names a scene the story has not saved. Nothing is stored: the batch either
    /// applies whole or not at all.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    Task SaveAsync(
        IReadOnlyDictionary<SceneId, ScenePosition> positions, CancellationToken cancellationToken = default);
}
