namespace Fourthwall.Application;

/// <summary>
/// The result of validating a story: every violation found, in the order the rules ran.
/// </summary>
public sealed record ValidationReport
{
    /// <summary>
    /// Initializes a new report.
    /// </summary>
    /// <param name="violations">The violations found, which may be empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="violations"/> is <see langword="null"/>.</exception>
    public ValidationReport(IReadOnlyList<ValidationViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        Violations = violations;
    }

    /// <summary>
    /// Gets every violation found.
    /// </summary>
    public IReadOnlyList<ValidationViolation> Violations { get; }

    /// <summary>
    /// Gets a value indicating whether the story is structurally valid, which is true when no
    /// violation is an error. Warnings do not invalidate a story.
    /// </summary>
    public bool IsValid => !Violations.Any(violation => violation.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Gets the violations that make the story invalid.
    /// </summary>
    public IEnumerable<ValidationViolation> Errors =>
        Violations.Where(violation => violation.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Gets the violations that flag something unintended without invalidating the story.
    /// </summary>
    public IEnumerable<ValidationViolation> Warnings =>
        Violations.Where(violation => violation.Severity == ValidationSeverity.Warning);
}
