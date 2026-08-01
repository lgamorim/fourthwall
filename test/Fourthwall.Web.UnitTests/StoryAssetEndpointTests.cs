using System.Text;

using Fourthwall.Web.Composition;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class StoryAssetEndpointTests
{
    private readonly FakeStoryWorkspace _workspace = new();

    [Fact]
    public async Task Should_NotFound_When_NoStoryIsOpen()
    {
        // Arrange, Act & Assert
        var result = await StoryAssetEndpoint.HandleAsync(
            _workspace, "assets/anything.png", TestContext.Current.CancellationToken);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Should_NotFound_When_TheAssetDoesNotExist()
    {
        // Arrange — a scene may reference an asset that is gone; that is a warning, not a crash.
        await OpenStoryAsync();

        // Act
        var result = await StoryAssetEndpoint.HandleAsync(
            _workspace, "assets/missing.png", TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Should_NotFound_When_ThePathIsBlank()
    {
        // Arrange
        await OpenStoryAsync();

        // Act
        var result = await StoryAssetEndpoint.HandleAsync(
            _workspace, "   ", TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Should_NotFound_When_TheExtensionIsNotAnImage()
    {
        // Arrange — only the image types the editor ingests are served, so a hand-edited path
        // cannot turn this endpoint into a general file reader for the story folder.
        var cancellationToken = TestContext.Current.CancellationToken;
        await OpenStoryAsync();
        var path = await _workspace.AssetStore.IngestAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("secret")), "txt", cancellationToken);

        // Act
        var result = await StoryAssetEndpoint.HandleAsync(_workspace, path, cancellationToken);

        // Assert
        Assert.IsType<NotFound>(result);
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("gif", "image/gif")]
    [InlineData("webp", "image/webp")]
    public async Task Should_ServeTheAsset_When_ItExists(string extension, string expectedContentType)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await OpenStoryAsync();
        var bytes = Encoding.UTF8.GetBytes("pixels");
        var path = await _workspace.AssetStore.IngestAsync(
            new MemoryStream(bytes), extension, cancellationToken);

        // Act
        var result = await StoryAssetEndpoint.HandleAsync(_workspace, path, cancellationToken);

        // Assert
        var file = Assert.IsAssignableFrom<IContentTypeHttpResult>(result);
        Assert.Equal(expectedContentType, file.ContentType);
    }

    [Fact]
    public async Task Should_MarkTheResponseImmutable_When_TheAssetIsServed()
    {
        // Arrange — safe only because asset names are content hashes: the bytes behind a name
        // never change.
        var cancellationToken = TestContext.Current.CancellationToken;
        await OpenStoryAsync();
        var path = await _workspace.AssetStore.IngestAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("pixels")), "png", cancellationToken);
        var result = await StoryAssetEndpoint.HandleAsync(_workspace, path, cancellationToken);
        var context = NewHttpContext();

        // Act
        await result.ExecuteAsync(context);

        // Assert
        Assert.Contains("immutable", context.Response.Headers.CacheControl.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_NotCacheTheAbsence_When_TheAssetIsMissing()
    {
        // A cached 404 would outlive the asset appearing later.
        var cancellationToken = TestContext.Current.CancellationToken;
        await OpenStoryAsync();
        var result = await StoryAssetEndpoint.HandleAsync(_workspace, "assets/missing.png", cancellationToken);
        var context = NewHttpContext();

        await result.ExecuteAsync(context);

        Assert.DoesNotContain("immutable", context.Response.Headers.CacheControl.ToString(), StringComparison.Ordinal);
    }

    // Executing a result resolves framework services (logging), which a bare context has none of.
    private static DefaultHttpContext NewHttpContext() => new()
    {
        RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        Response = { Body = new MemoryStream() },
    };

    private Task OpenStoryAsync() =>
        _workspace.CreateAsync(@"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken);
}
