using System.Text;

namespace Fourthwall.Application.UnitTests;

public sealed class InMemoryAssetStoreTests
{
    [Fact]
    public async Task Should_ReturnStoryRelativePathCarryingExtension_When_Ingested()
    {
        var store = new InMemoryAssetStore();

        var path = await store.IngestAsync(Content("pixels"), "png", TestContext.Current.CancellationToken);

        Assert.StartsWith("assets/", path);
        Assert.EndsWith(".png", path);
    }

    [Fact]
    public async Task Should_ReturnSamePathAndStoreOnce_When_IngestingIdenticalContent()
    {
        var store = new InMemoryAssetStore();

        var first = await store.IngestAsync(Content("same"), "png", TestContext.Current.CancellationToken);
        var second = await store.IngestAsync(Content("same"), "png", TestContext.Current.CancellationToken);
        var all = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Single(all);
    }

    [Fact]
    public async Task Should_ReturnDifferentPaths_When_ContentDiffers()
    {
        var store = new InMemoryAssetStore();

        var first = await store.IngestAsync(Content("one"), "png", TestContext.Current.CancellationToken);
        var second = await store.IngestAsync(Content("two"), "png", TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Should_ResolveIngestedPath_And_NotResolveOthers()
    {
        var store = new InMemoryAssetStore();
        var path = await store.IngestAsync(Content("pixels"), "png", TestContext.Current.CancellationToken);

        Assert.True(await store.ExistsAsync(path, TestContext.Current.CancellationToken));
        Assert.False(await store.ExistsAsync("assets/missing.png", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_NothingIngested()
    {
        var store = new InMemoryAssetStore();

        var all = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(all);
    }

    [Fact]
    public async Task Should_ListExactlyTheIngestedPaths()
    {
        var store = new InMemoryAssetStore();
        var first = await store.IngestAsync(Content("one"), "png", TestContext.Current.CancellationToken);
        var second = await store.IngestAsync(Content("two"), "jpg", TestContext.Current.CancellationToken);

        var all = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { first, second }.Order(), all.Order());
    }

    [Fact]
    public async Task Should_Throw_When_IngestingNullContent()
    {
        var store = new InMemoryAssetStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.IngestAsync(null!, "png", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_ExtensionIsBlank(string extension)
    {
        var store = new InMemoryAssetStore();

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.IngestAsync(Content("pixels"), extension, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_ExistsPathIsBlank(string? relativePath)
    {
        var store = new InMemoryAssetStore();

        // A null path throws ArgumentNullException, a blank one ArgumentException; both derive
        // from ArgumentException, which is the contract these guards promise.
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => store.ExistsAsync(relativePath!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_IngestIsAlreadyCancelled()
    {
        var store = new InMemoryAssetStore();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.IngestAsync(Content("pixels"), "png", cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_ExistsIsAlreadyCancelled()
    {
        var store = new InMemoryAssetStore();
        var path = await store.IngestAsync(Content("pixels"), "png", TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.ExistsAsync(path, cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_ListIsAlreadyCancelled()
    {
        var store = new InMemoryAssetStore();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.ListAsync(cancelled.Token));
    }

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));
}
