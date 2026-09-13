using Fourthwall.Application;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.UnitTests;

public class ValidationRulesTests
{
    [Theory]
    [InlineData(ValidationRule.SingleStartScene, "Start scene")]
    [InlineData(ValidationRule.AllScenesReachable, "Unreachable scenes")]
    [InlineData(ValidationRule.OutgoingDegreeMatchesKind, "Links don't match the kind")]
    [InlineData(ValidationRule.EndingReachable, "No ending can be reached")]
    [InlineData(ValidationRule.EverySceneCanReachEnding, "Dead ends")]
    [InlineData(ValidationRule.BrokenImageReference, "Missing image")]
    [InlineData(ValidationRule.OrphanAsset, "Unused image")]
    public void Should_NameTheRuleInTheCreatorsWords_When_Labelled(ValidationRule rule, string expected)
    {
        // Arrange & Act
        var label = ValidationRules.Label(rule);

        // Assert
        Assert.Equal(expected, label);
    }

    [Fact]
    public void Should_LabelEveryRule_When_TheEnumGrows()
    {
        // Arrange — a rule added to the Application enum must not surface as its member name.
        var rules = Enum.GetValues<ValidationRule>();

        // Act
        var labels = rules.Select(ValidationRules.Label).ToList();

        // Assert
        Assert.All(rules, rule => Assert.NotEqual(rule.ToString(), ValidationRules.Label(rule)));
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Should_Throw_When_TheRuleIsUnknown()
    {
        // Arrange
        var unknown = (ValidationRule)int.MaxValue;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationRules.Label(unknown));
    }
}
