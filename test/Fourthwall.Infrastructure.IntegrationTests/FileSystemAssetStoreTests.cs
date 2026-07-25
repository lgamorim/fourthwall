using System.Text;

namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class FileSystemAssetStoreTests : IDisposable
{
    private readonly string _storyFolder;
    private readonly FileSystemAssetStore _store;

    public FileSystemAssetStoreTests()
    {
        _storyFolder = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_storyFolder);
        _store = new FileSystemAssetStore(_storyFolder);
    }

    [Fact]
    public async Task Should_WriteHashedAssetAndResolveIt_When_Ingested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = Encoding.UTF8.GetBytes("a tiny png");

        var path = await _store.IngestAsync(StreamOf(bytes), "png", cancellationToken);

        Assert.StartsWith("assets/", path);
        Assert.EndsWith(".png", path);
        Assert.True(await _store.ExistsAsync(path, cancellationToken));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(FullPath(path), cancellationToken));
    }

    [Fact]
    public async Task Should_ReturnSamePathAndOneFile_When_IdenticalContentIngestedTwice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = Encoding.UTF8.GetBytes("same bytes");

        var first = await _store.IngestAsync(StreamOf(bytes), "png", cancellationToken);
        var second = await _store.IngestAsync(StreamOf(bytes), "png", cancellationToken);

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(Path.Combine(_storyFolder, "assets")));
    }

    [Fact]
    public async Task Should_ReturnDifferentPaths_When_SameContentDifferentExtension()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = Encoding.UTF8.GetBytes("same bytes");

        var png = await _store.IngestAsync(StreamOf(bytes), "png", cancellationToken);
        var jpg = await _store.IngestAsync(StreamOf(bytes), "jpg", cancellationToken);

        Assert.NotEqual(png, jpg);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_storyFolder, "assets")).Length);
    }

    [Fact]
    public async Task Should_ReturnDifferentPaths_When_ContentDiffers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var one = await _store.IngestAsync(StreamOf(Encoding.UTF8.GetBytes("one")), "png", cancellationToken);
        var two = await _store.IngestAsync(StreamOf(Encoding.UTF8.GetBytes("two")), "png", cancellationToken);

        Assert.NotEqual(one, two);
    }

    [Fact]
    public async Task Should_LowerCaseExtension_When_Ingested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var path = await _store.IngestAsync(StreamOf(Encoding.UTF8.GetBytes("x")), "PNG", cancellationToken);

        Assert.EndsWith(".png", path);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_AssetWasNeverIngested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.False(await _store.ExistsAsync("assets/does-not-exist.png", cancellationToken));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("assets/../../escape.png")]
    public async Task Should_ReturnFalse_When_PathEscapesStoryFolder(string relativePath)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.False(await _store.ExistsAsync(relativePath, cancellationToken));
    }

    [Fact]
    public async Task Should_BeEmpty_When_NothingIngested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(await _store.ListAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_ListExactlyTheIngestedPaths_When_Listed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var one = await _store.IngestAsync(StreamOf(Encoding.UTF8.GetBytes("one")), "png", cancellationToken);
        var two = await _store.IngestAsync(StreamOf(Encoding.UTF8.GetBytes("two")), "jpg", cancellationToken);

        var listed = await _store.ListAsync(cancellationToken);

        Assert.Equal(new[] { one, two }.Order(), listed.Order());
    }

    [Fact]
    public async Task Should_NotListTempArtifacts_When_InterruptedIngestLeftOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ingested = await _store.IngestAsync(StreamOf(Encoding.UTF8.GetBytes("real")), "png", cancellationToken);
        // A crash mid-ingest could leave a .tmp behind; it must never surface as an asset.
        await File.WriteAllTextAsync(
            Path.Combine(_storyFolder, "assets", $"{Guid.NewGuid():N}.tmp"), "partial", cancellationToken);

        var listed = await _store.ListAsync(cancellationToken);

        Assert.Equal([ingested], listed);
    }

    [Fact]
    public async Task Should_Throw_When_ContentIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _store.IngestAsync(null!, "png", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pn.g")]
    [InlineData("pn/g")]
    [InlineData("pn\\g")]
    public async Task Should_Throw_When_ExtensionIsBlankOrUnsafe(string fileExtension)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.IngestAsync(StreamOf([1, 2, 3]), fileExtension, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_ExistsPathIsBlank(string relativePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.ExistsAsync(relativePath, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Throw_When_StoryFolderIsNullOrBlank(string? storyFolder)
    {
        // null yields ArgumentNullException (a subclass), blank yields ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => new FileSystemAssetStore(storyFolder!));
    }

    [Fact]
    public async Task Should_Throw_When_IngestIsAlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _store.IngestAsync(StreamOf([1, 2, 3]), "png", cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_ExistsIsAlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _store.ExistsAsync("assets/x.png", cancelled.Token));
    }

    [Fact]
    public async Task Should_Throw_When_ListIsAlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.ListAsync(cancelled.Token));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_storyFolder, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle during teardown must not fail the test; the temp folder is disposable.
        }
    }

    private static MemoryStream StreamOf(byte[] bytes) => new(bytes);

    private string FullPath(string relativePath) =>
        Path.Combine(_storyFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
