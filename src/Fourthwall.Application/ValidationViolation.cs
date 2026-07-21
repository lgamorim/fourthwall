using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// A single rule violation found while validating a story.
/// </summary>
public sealed record ValidationViolation
{
    /// <summary>
    /// Initializes a new violation.
    /// </summary>
    /// <param name="rule">The rule that was violated.</param>
    /// <param name="severity">How serious the violation is.</param>
    /// <param name="message">A human-readable description of the problem.</param>
    /// <param name="sceneIds">
    /// The scenes at fault, so the editor can navigate straight to them. Empty when the
    /// violation belongs to the story as a whole rather than to any particular scene.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="sceneIds"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="message"/> is blank.</exception>
    public ValidationViolation(
        ValidationRule rule,
        ValidationSeverity severity,
        string message,
        IReadOnlyList<SceneId> sceneIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(sceneIds);

        Rule = rule;
        Severity = severity;
        Message = message;
        SceneIds = sceneIds;
    }

    /// <summary>
    /// Gets the rule that was violated.
    /// </summary>
    public ValidationRule Rule { get; }

    /// <summary>
    /// Gets how serious this violation is.
    /// </summary>
    public ValidationSeverity Severity { get; }

    /// <summary>
    /// Gets a human-readable description of the problem.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the scenes at fault, which is empty for story-level violations.
    /// </summary>
    public IReadOnlyList<SceneId> SceneIds { get; }
}
