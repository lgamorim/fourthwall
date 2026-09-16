using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// What a validation report says about one scene: the worst severity among the violations that
/// name it, and those violations' rules, for the mark its page carries on the map
/// (docs/design/0002-visual-direction.md §13).
/// </summary>
/// <param name="Severity">The worst severity; an error outranks any number of warnings.</param>
/// <param name="Rules">The rules that name the scene, in report order, each once.</param>
public sealed record SceneFindings(ValidationSeverity Severity, IReadOnlyList<ValidationRule> Rules)
{
    /// <summary>
    /// Folds a report into findings by scene.
    /// </summary>
    /// <remarks>
    /// Violations name scenes, never links, so a link is blamed by incidence: the canvas marks
    /// every link leaving a scene found here. A violation that names no scene — an unused image,
    /// a missing start — belongs to the story and stays in the panel.
    /// </remarks>
    /// <param name="report">The report the panel shows, or <see langword="null"/> when it shows none.</param>
    /// <returns>The findings for every scene the report names; empty without a report.</returns>
    public static IReadOnlyDictionary<SceneId, SceneFindings> ByScene(ValidationReport? report)
    {
        var findings = new Dictionary<SceneId, SceneFindings>();

        foreach (var violation in report?.Violations ?? [])
        {
            foreach (var sceneId in violation.SceneIds)
            {
                findings[sceneId] = findings.TryGetValue(sceneId, out var found)
                    ? found.With(violation)
                    : new SceneFindings(violation.Severity, [violation.Rule]);
            }
        }

        return findings;
    }

    // ValidationSeverity orders Error before Warning, so the lower value is the worse one.
    private SceneFindings With(ValidationViolation violation) => new(
        (ValidationSeverity)Math.Min((int)Severity, (int)violation.Severity),
        Rules.Contains(violation.Rule) ? Rules : [.. Rules, violation.Rule]);
}
