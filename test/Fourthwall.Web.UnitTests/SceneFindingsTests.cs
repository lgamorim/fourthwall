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
        Assert.Equal([ValidationRule.AllScenesReachable], findings[Reef].Errors);
        Assert.Empty(findings[Reef].Warnings);
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
    public void Should_ReportAWarning_When_OnlyWarningsNameTheScene()
    {
        // Arrange
        var report = Report(Violation(ValidationRule.EverySceneCanReachEnding, ValidationSeverity.Warning, Reef));

        // Act
        var finding = SceneFindings.ByScene(report)[Reef];

        // Assert
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Equal([ValidationRule.EverySceneCanReachEnding], finding.Warnings);
    }

    [Fact]
    public void Should_ListTheRulesBySeverityInReportOrderOnce_When_ASceneBreaksSeveral()
    {
        // Arrange — the page's name says which problems need fixing and which need checking.
        var report = Report(
            Violation(ValidationRule.OutgoingDegreeMatchesKind, ValidationSeverity.Error, Reef),
            Violation(ValidationRule.EverySceneCanReachEnding, ValidationSeverity.Warning, Reef, Reef),
            Violation(ValidationRule.AllScenesReachable, ValidationSeverity.Error, Reef),
            Violation(ValidationRule.OutgoingDegreeMatchesKind, ValidationSeverity.Error, Reef));

        // Act
        var finding = SceneFindings.ByScene(report)[Reef];

        // Assert
        Assert.Equal([ValidationRule.OutgoingDegreeMatchesKind, ValidationRule.AllScenesReachable], finding.Errors);
        Assert.Equal([ValidationRule.EverySceneCanReachEnding], finding.Warnings);
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

    [Fact]
    public void Should_Throw_When_AFindingNamesNoRule()
    {
        // Act & Assert — a scene with no problem has no finding, rather than one that claims a warning.
        Assert.Throws<ArgumentException>(() => new SceneFindings([], []));
    }

    [Fact]
    public void Should_Throw_When_ErrorsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneFindings(null!, [ValidationRule.BrokenImageReference]));
    }

    [Fact]
    public void Should_Throw_When_WarningsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneFindings([ValidationRule.AllScenesReachable], null!));
    }

    private static ValidationReport Report(params ValidationViolation[] violations) => new(violations);

    private static ValidationViolation Violation(
        ValidationRule rule, ValidationSeverity severity, params SceneId[] sceneIds) =>
        new(rule, severity, "Something is wrong.", sceneIds);
}
