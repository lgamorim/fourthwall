namespace Fourthwall.Application;

/// <summary>
/// How serious a validation violation is.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>The story is structurally invalid and cannot be considered finished.</summary>
    Error,

    /// <summary>The story is usable, but something looks unintended.</summary>
    Warning,
}
