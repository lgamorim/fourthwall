namespace Fourthwall.Web.Components;

/// <summary>
/// Classifies the failures a page shows as a message rather than letting them reach the error
/// boundary.
/// </summary>
/// <remarks>
/// Everything a creator can get wrong about a story folder — it already holds a story, it holds
/// none, its database cannot be read, it is not writable — arrives as one of these. Shared so the
/// picker and the editor draw the line in the same place.
/// </remarks>
public static class UserFacingFailures
{
    /// <summary>
    /// Determines whether a failure belongs on the page.
    /// </summary>
    /// <param name="exception">The failure to classify.</param>
    /// <returns><see langword="true"/> when the page should show it as a message.</returns>
    public static bool Includes(Exception exception) =>
        exception is InvalidOperationException or IOException or UnauthorizedAccessException;
}
