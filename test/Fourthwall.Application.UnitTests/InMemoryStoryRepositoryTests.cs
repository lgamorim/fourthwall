using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

public sealed class InMemoryStoryRepositoryTests
{
    [Fact]
    public async Task Should_ReturnNull_When_NothingHasBeenSaved()
    {
        var repository = new InMemoryStoryRepository();

        var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Should_ReturnSavedStory_When_LoadFollowsSave()
    {
        var repository = new InMemoryStoryRepository();
        var story = new Story("The Cave");

        await repository.SaveAsync(story, TestContext.Current.CancellationToken);
        var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Same(story, loaded);
    }

    [Fact]
    public async Task Should_ReplacePriorStory_When_SavedAgain()
    {
        var repository = new InMemoryStoryRepository();
        var first = new Story("First");
        var second = new Story("Second");

        await repository.SaveAsync(first, TestContext.Current.CancellationToken);
        await repository.SaveAsync(second, TestContext.Current.CancellationToken);
        var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Same(second, loaded);
    }

    [Fact]
    public async Task Should_Throw_When_SavingNullStory()
    {
        var repository = new InMemoryStoryRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => repository.SaveAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_SaveIsAlreadyCancelled()
    {
        var repository = new InMemoryStoryRepository();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.SaveAsync(new Story("Cancelled"), cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_LoadIsAlreadyCancelled()
    {
        var repository = new InMemoryStoryRepository();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.LoadAsync(cancelled.Token));
    }
}
