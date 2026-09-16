using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// What a validation report says about one scene: the rules it breaks, by severity, for the mark
/// its page carries on the map and the name that page is announced by
/// (docs/design/0002-visual-direction.md §13). A finding names at least one rule; a scene the
/// report does not blame has no finding at all.
/// </summary>
public sealed record SceneFindings
{
    /// <summary>
    /// Initializes a finding.
    /// </summary>
    /// <param name="errors">The rules the scene breaks as errors, in report order, each once.</param>
    /// <param name="warnings">The rules the scene breaks as warnings, in report order, each once.</param>
    /// <exception cref="ArgumentNullException">Either list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Both lists are empty.</exception>
    public SceneFindings(IReadOnlyList<ValidationRule> errors, IReadOnlyList<ValidationRule> warnings)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        if (errors.Count == 0 && warnings.Count == 0)
        {
            throw new ArgumentException("A finding names at least one rule.", nameof(errors));
        }

        Errors = errors;
        Warnings = warnings;
    }

    /// <summary>
    /// Gets the rules the scene breaks as errors, in report order, each once.
    /// </summary>
    public IReadOnlyList<ValidationRule> Errors { get; }

    /// <summary>
    /// Gets the rules the scene breaks as warnings, in report order, each once.
    /// </summary>
    public IReadOnlyList<ValidationRule> Warnings { get; }

    /// <summary>
    /// Gets the worst severity: an error outranks any number of warnings.
    /// </summary>
    public ValidationSeverity Severity => Errors.Count > 0 ? ValidationSeverity.Error : ValidationSeverity.Warning;

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
                    ? With(found.Errors, found.Warnings, violation)
                    : With([], [], violation);
            }
        }

        return findings;
    }

    // Exhaustive, like the canvas's kind switches: a severity the editor does not know is a defect.
    private static SceneFindings With(
        IReadOnlyList<ValidationRule> errors, IReadOnlyList<ValidationRule> warnings, ValidationViolation violation) =>
        violation.Severity switch
        {
            ValidationSeverity.Error => new SceneFindings(Adding(errors, violation.Rule), warnings),
            ValidationSeverity.Warning => new SceneFindings(errors, Adding(warnings, violation.Rule)),
            _ => throw new InvalidOperationException($"Unknown validation severity '{violation.Severity}'."),
        };

    private static IReadOnlyList<ValidationRule> Adding(IReadOnlyList<ValidationRule> rules, ValidationRule rule) =>
        rules.Contains(rule) ? rules : [.. rules, rule];
}
