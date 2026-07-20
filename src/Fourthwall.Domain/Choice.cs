namespace Fourthwall.Domain;

/// <summary>
/// A labelled transition a reader can take from one scene to another.
/// </summary>
/// <remarks>
/// Choices are first-class: the label is authored independently of the scene it leads to,
/// and a choice always points at a scene.
/// </remarks>
public sealed record Choice
{
    /// <summary>
    /// Initializes a new choice.
    /// </summary>
    /// <param name="label">The reader-facing text of the choice, such as "Open the door".</param>
    /// <param name="targetSceneId">The scene this choice leads to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="label"/> is blank, or <paramref name="targetSceneId"/> is the default value.
    /// </exception>
    public Choice(string label, SceneId targetSceneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        if (targetSceneId == default)
        {
            throw new ArgumentException("A choice must point at a scene.", nameof(targetSceneId));
        }

        Label = label;
        TargetSceneId = targetSceneId;
    }

    /// <summary>
    /// Gets the reader-facing text of this choice.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the identifier of the scene this choice leads to.
    /// </summary>
    public SceneId TargetSceneId { get; }
}
