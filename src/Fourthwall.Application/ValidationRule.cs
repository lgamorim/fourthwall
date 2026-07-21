namespace Fourthwall.Application;

/// <summary>
/// The structural rules a story is checked against (design doc section 4.2).
/// </summary>
public enum ValidationRule
{
    /// <summary>The story must have a start scene.</summary>
    SingleStartScene,

    /// <summary>Every scene must be reachable from the start scene.</summary>
    AllScenesReachable,

    /// <summary>A scene's number of outgoing transitions must match its kind.</summary>
    OutgoingDegreeMatchesKind,

    /// <summary>At least one ending must be reachable from the start scene.</summary>
    EndingReachable,

    /// <summary>Every scene should still be able to reach some ending.</summary>
    EverySceneCanReachEnding,
}
