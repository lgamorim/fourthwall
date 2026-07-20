namespace Fourthwall.Domain;

/// <summary>
/// A branching story: a set of scenes, the transitions between them, and the scene a reader starts from.
/// </summary>
/// <remarks>
/// The story is the aggregate root. Every operation that crosses scene boundaries — wiring a
/// choice, setting a follow-up, removing a scene — goes through it, because it is the only place
/// referential integrity between scenes can be enforced.
/// <para>
/// The story guarantees that transitions never dangle and that the start scene, when set, belongs
/// to it. It deliberately permits states that are incomplete rather than corrupt (no start scene
/// yet, a choice scene with a single choice); those are reported by validation, not prevented here.
/// </para>
/// </remarks>
public sealed class Story
{
    private readonly Dictionary<SceneId, Scene> _scenes = [];

    /// <summary>
    /// Initializes a new, empty story.
    /// </summary>
    /// <param name="title">The story's title.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is blank.</exception>
    public Story(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
    }

    /// <summary>
    /// Gets the title of this story.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets every scene in this story.
    /// </summary>
    public IReadOnlyCollection<Scene> Scenes => _scenes.Values;

    /// <summary>
    /// Gets the scene a reader starts from, or <see langword="null"/> when none has been chosen yet.
    /// </summary>
    public SceneId? StartSceneId { get; private set; }

    /// <summary>
    /// Renames this story.
    /// </summary>
    /// <param name="title">The new title.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is blank.</exception>
    public void Rename(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
    }

    /// <summary>
    /// Creates a scene and adds it to this story.
    /// </summary>
    /// <param name="kind">The kind of scene to create.</param>
    /// <param name="text">The narrative text; may be empty, but not null.</param>
    /// <param name="outcome">The ending outcome, required when <paramref name="kind"/> is
    /// <see cref="SceneKind.Ending"/> and disallowed otherwise.</param>
    /// <returns>The scene that was added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="kind"/> is not defined, or <paramref name="outcome"/> does not match the kind.
    /// </exception>
    public Scene AddScene(SceneKind kind, string text, EndingOutcome? outcome = null)
    {
        var scene = new Scene(SceneId.New(), kind, text, outcome);
        _scenes.Add(scene.Id, scene);
        return scene;
    }

    /// <summary>
    /// Removes a scene, together with every transition pointing at it.
    /// </summary>
    /// <param name="sceneId">The scene to remove.</param>
    /// <returns>
    /// <see langword="true"/> when the scene was removed; <see langword="false"/> when this story
    /// has no such scene.
    /// </returns>
    /// <remarks>
    /// Removing the start scene leaves the story without one, which validation then reports.
    /// </remarks>
    public bool RemoveScene(SceneId sceneId)
    {
        if (!_scenes.Remove(sceneId))
        {
            return false;
        }

        foreach (var scene in _scenes.Values)
        {
            scene.RemoveEdgesTargeting(sceneId);
        }

        if (StartSceneId == sceneId)
        {
            StartSceneId = null;
        }

        return true;
    }

    /// <summary>
    /// Finds a scene by its identifier.
    /// </summary>
    /// <param name="sceneId">The scene to find.</param>
    /// <returns>The scene, or <see langword="null"/> when this story has no such scene.</returns>
    public Scene? FindScene(SceneId sceneId) => _scenes.GetValueOrDefault(sceneId);

    /// <summary>
    /// Sets the scene a reader starts from.
    /// </summary>
    /// <param name="sceneId">The scene to start from.</param>
    /// <exception cref="ArgumentException"><paramref name="sceneId"/> is not a scene in this story.</exception>
    public void SetStartScene(SceneId sceneId)
    {
        _ = RequireScene(sceneId, nameof(sceneId));
        StartSceneId = sceneId;
    }

    /// <summary>
    /// Adds a choice leading from one scene to another, after any existing choices.
    /// </summary>
    /// <param name="fromSceneId">The scene the choice leads from.</param>
    /// <param name="label">The reader-facing text of the choice.</param>
    /// <param name="targetSceneId">The scene the choice leads to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="label"/> is blank, or either scene is not part of this story.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The scene the choice leads from is not a <see cref="SceneKind.Choice"/> scene.
    /// </exception>
    public void WireChoice(SceneId fromSceneId, string label, SceneId targetSceneId)
    {
        var from = RequireScene(fromSceneId, nameof(fromSceneId));
        _ = RequireScene(targetSceneId, nameof(targetSceneId));

        from.AddChoice(new Choice(label, targetSceneId));
    }

    /// <summary>
    /// Removes the choice at the given position from a scene.
    /// </summary>
    /// <param name="sceneId">The scene holding the choice.</param>
    /// <param name="index">The zero-based position of the choice.</param>
    /// <exception cref="ArgumentException"><paramref name="sceneId"/> is not a scene in this story.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the scene's choices.</exception>
    public void RemoveChoice(SceneId sceneId, int index) =>
        RequireScene(sceneId, nameof(sceneId)).RemoveChoiceAt(index);

    /// <summary>
    /// Moves a choice to a different position, changing the order the reader sees.
    /// </summary>
    /// <param name="sceneId">The scene holding the choice.</param>
    /// <param name="fromIndex">The choice's current position.</param>
    /// <param name="toIndex">The position to move it to.</param>
    /// <exception cref="ArgumentException"><paramref name="sceneId"/> is not a scene in this story.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either index is outside the scene's choices.</exception>
    public void MoveChoice(SceneId sceneId, int fromIndex, int toIndex) =>
        RequireScene(sceneId, nameof(sceneId)).MoveChoice(fromIndex, toIndex);

    /// <summary>
    /// Sets the scene a linear scene flows into.
    /// </summary>
    /// <param name="fromSceneId">The linear scene.</param>
    /// <param name="targetSceneId">The scene it flows into.</param>
    /// <exception cref="ArgumentException">Either scene is not part of this story.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="fromSceneId"/> is not a <see cref="SceneKind.Linear"/> scene.
    /// </exception>
    public void SetFollowUp(SceneId fromSceneId, SceneId targetSceneId)
    {
        var from = RequireScene(fromSceneId, nameof(fromSceneId));
        _ = RequireScene(targetSceneId, nameof(targetSceneId));

        from.SetFollowUp(targetSceneId);
    }

    /// <summary>
    /// Clears the follow-up of a scene, if it has one.
    /// </summary>
    /// <param name="sceneId">The scene to clear.</param>
    /// <exception cref="ArgumentException"><paramref name="sceneId"/> is not a scene in this story.</exception>
    public void ClearFollowUp(SceneId sceneId) =>
        RequireScene(sceneId, nameof(sceneId)).ClearFollowUp();

    private Scene RequireScene(SceneId sceneId, string parameterName) =>
        _scenes.GetValueOrDefault(sceneId)
        ?? throw new ArgumentException("The scene is not part of this story.", parameterName);
}
