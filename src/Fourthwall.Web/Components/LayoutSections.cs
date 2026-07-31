namespace Fourthwall.Web.Components;

/// <summary>
/// Identities for the regions of the shell that a page fills in.
/// </summary>
/// <remarks>
/// Object identities rather than names, so the outlet and the content that fills it are bound by a
/// shared reference the compiler checks, not by a string the two sides can disagree about.
/// </remarks>
public static class LayoutSections
{
    /// <summary>
    /// The right-hand dock: the scene inspector, and from M16 the validation panel.
    /// </summary>
    public static readonly object Dock = new();
}
