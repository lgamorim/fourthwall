using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

/// <remarks>
/// Stands in for the SQLite-backed repository that arrives in M8, so code written against
/// <see cref="IStoryRepository"/> can be exercised without a database. It holds the last story
/// saved and hands it straight back — the simplest behaviour that honours the contract. It does
/// not reconstruct a fresh aggregate the way the real repository will (the Domain has no
/// rehydration path yet); round-trip fidelity is proven by M8's integration tests, not here.
/// </remarks>
internal sealed class InMemoryStoryRepository : IStoryRepository
{
    private Story? _story;

    public Task SaveAsync(Story story, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(story);

        _story = story;
        return Task.CompletedTask;
    }

    public Task<Story?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_story);
    }
}
