using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

internal sealed class InMemoryStoryGraphFactory : IStoryGraphFactory
{
    public IStoryGraph Create(Story story) => new InMemoryStoryGraph(story);
}
