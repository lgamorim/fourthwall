using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Structural queries over a story's scenes and the transitions between them.
/// </summary>
/// <remarks>
/// A story is a general directed graph — cycles are legal — so implementations must terminate
/// on cyclic stories. Implementations translate scene identifiers in and analysis results out;
/// no graph-library type ever crosses this boundary.
/// </remarks>
public interface IStoryGraph
{
    /// <summary>
    /// Finds every scene reachable by following transitions forward from a scene.
    /// </summary>
    /// <param name="origin">The scene to walk from.</param>
    /// <returns>
    /// The reachable scenes, <b>including <paramref name="origin"/> itself</b>: reachability is
    /// reflexive, so a start scene counts as reachable from the start.
    /// </returns>
    IReadOnlySet<SceneId> ReachableFrom(SceneId origin);

    /// <summary>
    /// Finds every scene from which at least one of the given scenes can be reached.
    /// </summary>
    /// <param name="targets">The scenes to walk backward from.</param>
    /// <returns>
    /// The scenes that can reach a target, <b>including the targets themselves</b>: an ending
    /// trivially reaches an ending. Empty when <paramref name="targets"/> is empty.
    /// </returns>
    IReadOnlySet<SceneId> ScenesThatCanReachAny(IReadOnlySet<SceneId> targets);
}
