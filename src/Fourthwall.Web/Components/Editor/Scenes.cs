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
    public static string Label(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (string.IsNullOrWhiteSpace(scene.Text))
        {
            return "(no text)";
        }

        var text = scene.Text.Trim();
        if (text.Length <= LabelLength)
        {
            return text;
        }

        // Back off a character when the cut would land inside a surrogate pair, so the label never
        // ends in half an emoji.
        var length = char.IsHighSurrogate(text[LabelLength - 1]) ? LabelLength - 1 : LabelLength;
        return string.Concat(text.AsSpan(0, length), "…");
    }
}
