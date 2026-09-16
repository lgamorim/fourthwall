using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Canvas;

namespace Fourthwall.Web.UnitTests;

public class SceneFindingsTests
{
    private static readonly SceneId Harbour = SceneId.New();
    private static readonly SceneId Reef = SceneId.New();

    [Fact]
    public void Should_FindNothing_When_ThereIsNoReport()
    {
        // Act
        var findings = SceneFindings.ByScene(null);

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Should_NameEveryBlamedScene_When_AViolationNamesSeveral()
    {
        // Arrange
        var report = Report(Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, Harbour, Reef));

        // Act
        var findings = SceneFindings.ByScene(report);

        // Assert
        Assert.Equal(ValidationSeverity.Error, findings[Harbour].Severity);
        Assert.Equal(ValidationSeverity.Error, findings[Reef].Severity);
        Assert.Equal([ValidationRule.AllScenesReachable], findings[Reef].Rules);
    }

    [Fact]
    public void Should_KeepTheError_When_ASceneHasAWarningToo()
    {
        // Arrange — the warning comes first, so the order of the report cannot decide.
        var report = Report(
            Violation(ValidationRule.EverySceneCanReachEnding, ValidationSeverity.Warning, Reef),
            Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, Reef),
            Violation(ValidationRule.BrokenImageReference, ValidationSeverity.Warning, Reef));

        // Act
        var finding = SceneFindings.ByScene(report)[Reef];

        // Assert
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
    }

    [Fact]
    public void Should_ListTheRulesInReportOrderOnce_When_ASceneBreaksSeveral()
    {
        // Arrange
        var report = Report(
            Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, Reef),
            Violation(ValidationRule.EverySceneCanReachEnding, ValidationSeverity.Warning, Reef, Reef));

        // Act
        var finding = SceneFindings.ByScene(report)[Reef];

        // Assert
        Assert.Equal([ValidationRule.AllScenesReachable, ValidationRule.EverySceneCanReachEnding], finding.Rules);
    }

    [Fact]
    public void Should_BlameNoScene_When_AViolationNamesNone()
    {
        // Arrange — an unused image and a missing start belong to the story, not to a scene.
        var report = Report(
            Violation(ValidationRule.OrphanAsset, ValidationSeverity.Warning),
            Violation(ValidationRule.SingleStartScene, ValidationSeverity.Error));

        // Act
        var findings = SceneFindings.ByScene(report);

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Should_LeaveOtherScenesOut_When_OnlyOneIsBlamed()
    {
        // Arrange
        var report = Report(Violation(ValidationRule.EverySceneCanReachEnding, ValidationSeverity.Warning, Reef));

        // Act
        var findings = SceneFindings.ByScene(report);

        // Assert
        Assert.False(findings.ContainsKey(Harbour));
    }

    private static ValidationReport Report(params ValidationViolation[] violations) => new(violations);

    private static ValidationViolation Violation(
        ValidationRule rule, ValidationSeverity severity, params SceneId[] sceneIds) =>
        new(rule, severity, "Something is wrong.", sceneIds);
}
