namespace Fourthwall.Application.UnitTests;

public class ValidationReportTests
{
    [Fact]
    public void Should_BeValidWithNoViolations_When_ReportIsEmpty()
    {
        // Arrange & Act
        var report = new ValidationReport([]);

        // Assert
        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
        Assert.Empty(report.Errors);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Should_BeValid_When_OnlyWarningsAreReported()
    {
        // Warnings never invalidate a story (design doc section 4.2).

        // Arrange
        var warning = Violation(ValidationSeverity.Warning);

        // Act
        var report = new ValidationReport([warning]);

        // Assert
        Assert.True(report.IsValid);
        Assert.Single(report.Warnings);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void Should_BeInvalid_When_AnyErrorIsReported()
    {
        // Arrange
        var error = Violation(ValidationSeverity.Error);
        var warning = Violation(ValidationSeverity.Warning);

        // Act
        var report = new ValidationReport([warning, error]);

        // Assert
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Should_PartitionViolations_When_BothSeveritiesPresent()
    {
        // Arrange
        var error = Violation(ValidationSeverity.Error);
        var warning = Violation(ValidationSeverity.Warning);

        // Act
        var report = new ValidationReport([error, warning]);

        // Assert
        Assert.Equal([error], report.Errors);
        Assert.Equal([warning], report.Warnings);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_ViolationsIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ValidationReport(null!));
    }

    private static ValidationViolation Violation(ValidationSeverity severity) =>
        new(ValidationRule.SingleStartScene, severity, "message", []);
}
