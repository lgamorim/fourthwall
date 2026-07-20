namespace Fourthwall.Domain;

/// <summary>
/// A single passage of a story: narrative text, an optional image, and the
/// outgoing transitions permitted by its <see cref="SceneKind"/>.
/// </summary>
/// <remarks>
/// Scenes are normally created through <see cref="Story.AddScene"/>, which registers them with
/// their story. Transitions are mutated only by the owning <see cref="Story"/>, which is the only
/// place referential integrity between scenes can be enforced.
/// <para>
/// This type enforces a scene's <em>shape</em> — an ending carries an outcome and has no
/// outgoing transitions. It deliberately does not enforce <em>cardinality</em> (a choice scene
/// having at least two choices, a linear scene having exactly one follow-up), because a scene
/// under construction is legitimately incomplete; those are validation rules.
/// </para>
/// </remarks>
public sealed class Scene
{
    private readonly List<Choice> _choices = [];

    /// <summary>
    /// Initializes a new scene.
    /// </summary>
    /// <param name="id">The scene's identifier.</param>
    /// <param name="kind">The kind of scene.</param>
    /// <param name="text">The narrative text; may be empty while authoring, but not null.</param>
    /// <param name="outcome">The ending outcome; required when <paramref name="kind"/> is
    /// <see cref="SceneKind.Ending"/> and disallowed otherwise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is the default value, <paramref name="kind"/> is not a defined
    /// <see cref="SceneKind"/>, or <paramref name="outcome"/> does not match the kind.
    /// </exception>
    public Scene(SceneId id, SceneKind kind, string text, EndingOutcome? outcome = null)
    {
        if (id == default)
        {
            throw new ArgumentException("A scene must have an identifier.", nameof(id));
        }

        EnsureDefined(kind, nameof(kind));
        ArgumentNullException.ThrowIfNull(text);
        EnsureOutcomeMatchesKind(kind, outcome, nameof(outcome));

        Id = id;
        Kind = kind;
        Text = text;
        Outcome = outcome;
    }

    /// <summary>
    /// Gets the identifier of this scene.
    /// </summary>
    public SceneId Id { get; }

    /// <summary>
    /// Gets the kind of this scene.
    /// </summary>
    public SceneKind Kind { get; private set; }

    /// <summary>
    /// Gets the narrative text of this scene.
    /// </summary>
    public string Text { get; private set; }

    /// <summary>
    /// Gets the story-relative path of this scene's image, or <see langword="null"/> when none is attached.
    /// </summary>
    public string? ImagePath { get; private set; }

    /// <summary>
    /// Gets the outcome of this scene, set only when it is an ending.
    /// </summary>
    public EndingOutcome? Outcome { get; private set; }

    /// <summary>
    /// Gets the choices leading out of this scene, in reader-facing order.
    /// </summary>
    public IReadOnlyList<Choice> Choices => _choices;

    /// <summary>
    /// Gets the scene this one flows into, set only when it is a linear scene.
    /// </summary>
    public SceneId? FollowUpSceneId { get; private set; }

    /// <summary>
    /// Gets every scene reachable directly from this one, whether through a choice or a follow-up.
    /// </summary>
    public IEnumerable<SceneId> OutgoingSceneIds
    {
        get
        {
            foreach (var choice in _choices)
            {
                yield return choice.TargetSceneId;
            }

            if (FollowUpSceneId is { } followUp)
            {
                yield return followUp;
            }
        }
    }

    /// <summary>
    /// Replaces the narrative text of this scene.
    /// </summary>
    /// <param name="text">The new text; may be empty, but not null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>
    /// Attaches an image to this scene.
    /// </summary>
    /// <param name="relativePath">The story-relative path of the image asset.</param>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is blank.</exception>
    public void AttachImage(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ImagePath = relativePath;
    }

    /// <summary>
    /// Removes the image attached to this scene, if any.
    /// </summary>
    public void ClearImage() => ImagePath = null;

    /// <summary>
    /// Changes the kind of this scene, discarding any outgoing transitions that the new kind
    /// cannot carry.
    /// </summary>
    /// <param name="kind">The new kind.</param>
    /// <param name="outcome">The ending outcome; required when changing to
    /// <see cref="SceneKind.Ending"/> and disallowed otherwise.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="kind"/> is not a defined <see cref="SceneKind"/>, or
    /// <paramref name="outcome"/> does not match the kind.
    /// </exception>
    public void ChangeKind(SceneKind kind, EndingOutcome? outcome = null)
    {
        EnsureDefined(kind, nameof(kind));
        EnsureOutcomeMatchesKind(kind, outcome, nameof(outcome));

        if (kind != Kind)
        {
            _choices.Clear();
            FollowUpSceneId = null;
        }

        Kind = kind;
        Outcome = outcome;
    }

    internal void AddChoice(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        EnsureKindIs(SceneKind.Choice, "hold choices");
        _choices.Add(choice);
    }

    internal void RemoveChoiceAt(int index)
    {
        EnsureChoiceIndexInRange(index);
        _choices.RemoveAt(index);
    }

    internal void MoveChoice(int fromIndex, int toIndex)
    {
        EnsureChoiceIndexInRange(fromIndex);
        EnsureChoiceIndexInRange(toIndex);

        var choice = _choices[fromIndex];
        _choices.RemoveAt(fromIndex);
        _choices.Insert(toIndex, choice);
    }

    internal void SetFollowUp(SceneId target)
    {
        EnsureKindIs(SceneKind.Linear, "have a follow-up scene");
        FollowUpSceneId = target;
    }

    internal void ClearFollowUp() => FollowUpSceneId = null;

    internal void RemoveEdgesTargeting(SceneId target)
    {
        _choices.RemoveAll(choice => choice.TargetSceneId == target);

        if (FollowUpSceneId == target)
        {
            FollowUpSceneId = null;
        }
    }

    private static void EnsureDefined(SceneKind kind, string parameterName)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException($"'{kind}' is not a defined scene kind.", parameterName);
        }
    }

    private static void EnsureOutcomeMatchesKind(SceneKind kind, EndingOutcome? outcome, string parameterName)
    {
        switch (kind)
        {
            case SceneKind.Ending when outcome is null:
                throw new ArgumentException("An ending scene must carry an outcome.", parameterName);
            case not SceneKind.Ending when outcome is not null:
                throw new ArgumentException("Only an ending scene may carry an outcome.", parameterName);
            default:
                break;
        }
    }

    private void EnsureKindIs(SceneKind required, string action)
    {
        if (Kind != required)
        {
            throw new InvalidOperationException($"Only a {required} scene may {action}; this scene is {Kind}.");
        }
    }

    private void EnsureChoiceIndexInRange(int index)
    {
        if (index < 0 || index >= _choices.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"The scene has {_choices.Count} choice(s).");
        }
    }
}
