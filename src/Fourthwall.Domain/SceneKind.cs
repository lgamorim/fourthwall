namespace Fourthwall.Domain;

/// <summary>
/// How a scene ends, and therefore what outgoing transitions it may have.
/// </summary>
public enum SceneKind
{
    /// <summary>The scene offers the reader labelled choices, each leading to another scene.</summary>
    Choice,

    /// <summary>The scene flows into a single follow-up scene.</summary>
    Linear,

    /// <summary>The scene terminates the story and carries an outcome.</summary>
    Ending,
}
