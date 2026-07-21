using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Checks a story against the structural rules of design doc section 4.2.
/// </summary>
public interface IStoryValidator
{
    /// <summary>
    /// Validates a story, reporting every violation rather than stopping at the first.
    /// </summary>
    /// <param name="story">The story to validate.</param>
    /// <returns>A report of every violation found, empty when the story is sound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="story"/> is <see langword="null"/>.</exception>
    ValidationReport Validate(Story story);
}
