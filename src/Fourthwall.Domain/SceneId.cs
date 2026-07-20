namespace Fourthwall.Domain;

/// <summary>
/// Uniquely identifies a scene within a story.
/// </summary>
/// <param name="Value">The underlying identifier value.</param>
/// <remarks>
/// A default instance carries <see cref="Guid.Empty"/> and is never a valid target.
/// Because that cannot be prevented on a struct, usage sites reject it instead.
/// </remarks>
public readonly record struct SceneId(Guid Value)
{
    /// <summary>
    /// Creates a new, unique scene identifier.
    /// </summary>
    /// <returns>A freshly generated <see cref="SceneId"/>.</returns>
    public static SceneId New() => new(Guid.NewGuid());
}
