using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Editor;

/// <summary>
/// Builds ending outcomes from the pieces an editing form collects. Shared so the create form and
/// the inspector agree on when a label is required.
/// </summary>
public static class Outcomes
{
    /// <summary>
    /// Gets a value indicating whether the given outcome needs a label the creator must supply.
    /// </summary>
    /// <param name="kind">The outcome kind.</param>
    /// <returns><see langword="true"/> when a label is required.</returns>
    public static bool RequiresLabel(OutcomeKind kind) => kind == OutcomeKind.Other;

    /// <summary>
    /// Builds an outcome of the given kind.
    /// </summary>
    /// <param name="kind">The outcome kind.</param>
    /// <param name="label">The label; optional except when <paramref name="kind"/> requires one.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="kind"/> requires a label and <paramref name="label"/> is blank.
    /// </exception>
    public static EndingOutcome Build(OutcomeKind kind, string? label)
    {
        var trimmed = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        return kind switch
        {
            OutcomeKind.Death => EndingOutcome.Death(trimmed),
            OutcomeKind.Victory => EndingOutcome.Victory(trimmed),
            _ => EndingOutcome.Other(trimmed!),
        };
    }
}
