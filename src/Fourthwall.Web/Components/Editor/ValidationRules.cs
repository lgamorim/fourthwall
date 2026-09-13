using Fourthwall.Application;

namespace Fourthwall.Web.Components.Editor;

/// <summary>
/// Names validation rules in the creator's words.
/// </summary>
/// <remarks>
/// The Application enum names each rule by what it checks; the panel names it by what the creator
/// sees wrong. The mapping lives in one place so a rule added later cannot reach the screen as its
/// enum member name.
/// </remarks>
public static class ValidationRules
{
    /// <summary>
    /// Labels a rule for a validation row.
    /// </summary>
    /// <param name="rule">The rule to label.</param>
    /// <returns>The rule's name as a creator reads it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule is not one the editor knows.</exception>
    public static string Label(ValidationRule rule) => rule switch
    {
        ValidationRule.SingleStartScene => "Start scene",
        ValidationRule.AllScenesReachable => "Unreachable scenes",
        ValidationRule.OutgoingDegreeMatchesKind => "Links don't match the kind",
        ValidationRule.EndingReachable => "No ending can be reached",
        ValidationRule.EverySceneCanReachEnding => "Dead ends",
        ValidationRule.BrokenImageReference => "Missing image",
        ValidationRule.OrphanAsset => "Unused image",
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown validation rule."),
    };
}
