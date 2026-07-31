using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Editor;

/// <summary>
/// Builds ending outcomes from the pieces an editing form collects.
/// </summary>
/// <remarks>
/// Both the create form and the inspector build outcomes, so the rule about which kinds need a
/// label — and the wording a creator sees when one is missing — lives here rather than in two
/// copies that can drift apart.
/// </remarks>
public static class Outcomes
{
    /// <summary>
    /// Builds an outcome from a chosen kind and the label a form collected.
    /// </summary>
    /// <param name="kind">The outcome kind.</param>
    /// <param name="label">The label; optional except for kinds that require one.</param>
    /// <param name="outcome">The built outcome, or <see langword="null"/> when a label is missing.</param>
    /// <param name="error">
    /// The message to show the creator, or <see langword="null"/> when the outcome was built.
    /// </param>
    /// <returns><see langword="true"/> when the outcome was built.</returns>
    public static bool TryBuild(
        OutcomeKind kind, string? label, out EndingOutcome? outcome, out string? error)
    {
        var trimmed = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        if (RequiresLabel(kind) && trimmed is null)
        {
            outcome = null;
            error = $"An outcome of {kind} needs a label.";
            return false;
        }

        outcome = kind switch
        {
            OutcomeKind.Death => EndingOutcome.Death(trimmed),
            OutcomeKind.Victory => EndingOutcome.Victory(trimmed),
            // trimmed is non-null here: RequiresLabel covers exactly the kinds this arm serves, and
            // the guard above has already rejected a missing label.
            _ => EndingOutcome.Other(trimmed!),
        };

        error = null;
        return true;
    }

    private static bool RequiresLabel(OutcomeKind kind) => kind == OutcomeKind.Other;
}
