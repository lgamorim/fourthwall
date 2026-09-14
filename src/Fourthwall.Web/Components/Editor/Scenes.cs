using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Editor;

/// <summary>
/// How the editor presents a story's scenes: the order they are listed in, and the label that
/// stands for one.
/// </summary>
/// <remarks>
/// The scene list and every target dropdown must agree, so both rules live here rather than in a
/// copy per component.
/// </remarks>
public static class Scenes
{
    private const int LabelLength = 60;

    /// <summary>
    /// Orders a story's scenes for display.
    /// </summary>
    /// <param name="story">The story whose scenes to order.</param>
    /// <returns>The scenes, start scene first, then alphabetically by text.</returns>
    /// <remarks>
    /// <see cref="Story.Scenes"/> comes from a dictionary and has no inherent order, so the editor
    /// imposes one. The identifier breaks ties so scenes with identical text cannot swap places
    /// between renders.
    /// </remarks>
    public static IEnumerable<Scene> Ordered(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        return story.Scenes
            .OrderByDescending(scene => scene.Id == story.StartSceneId)
            .ThenBy(scene => scene.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scene => scene.Id.Value);
    }

    /// <summary>
    /// Labels a scene for a list row or a dropdown entry.
    /// </summary>
    /// <param name="scene">The scene to label.</param>
    /// <returns>A short label derived from the scene's text.</returns>
    /// <remarks>
    /// Scenes carry narrative text and no title, so the text itself is the label — truncated, and
    /// stood in for when it is empty, which is legal while authoring.
    /// </remarks>
    public static string Label(Scene scene) => Label(scene, LabelLength);

    /// <summary>
    /// Labels a scene where room is shorter than a list row — a canvas node, whose SVG text does not
    /// wrap.
    /// </summary>
    /// <param name="scene">The scene to label.</param>
    /// <param name="maxLength">The most characters of the scene's text to keep before the ellipsis.</param>
    /// <returns>A short label derived from the scene's text.</returns>
    public static string Label(Scene scene, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        return string.IsNullOrWhiteSpace(scene.Text) ? "(no text)" : Truncate(scene.Text, maxLength);
    }

    /// <summary>
    /// Shortens text to a maximum length, marking the cut with an ellipsis.
    /// </summary>
    /// <param name="text">The text to shorten; surrounding whitespace is dropped.</param>
    /// <param name="maxLength">The most characters to keep before the ellipsis.</param>
    /// <returns>The trimmed text when it fits; otherwise its first characters and an ellipsis.</returns>
    /// <remarks>
    /// Scene labels and choice labels on the canvas share this, so both cut the same way.
    /// </remarks>
    public static string Truncate(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var trimmed = text.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        // Back off a character when the cut would land inside a surrogate pair, so the label never
        // ends in half an emoji.
        var length = char.IsHighSurrogate(trimmed[maxLength - 1]) ? maxLength - 1 : maxLength;
        return string.Concat(trimmed.AsSpan(0, length), "…");
    }
}
