using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Builds a queryable graph from a story.
/// </summary>
/// <remarks>
/// Validation builds the graph once and queries it several times, so the cost of translating a
/// story into the underlying graph representation is paid once per run rather than per query.
/// </remarks>
public interface IStoryGraphFactory
{
    /// <summary>
    /// Builds a graph over the given story's scenes and transitions.
    /// </summary>
    /// <param name="story">The story to analyse.</param>
    /// <returns>A graph reflecting the story as it stands when this method is called.</returns>
    IStoryGraph Create(Story story);
}
