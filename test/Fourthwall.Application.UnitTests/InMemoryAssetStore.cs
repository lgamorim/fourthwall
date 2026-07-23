using System.Security.Cryptography;

namespace Fourthwall.Application.UnitTests;

/// <remarks>
/// Stands in for the file-backed asset store that arrives in M9, so code written against
/// <see cref="IAssetStore"/> can be exercised without touching the file system. It keeps the
/// ingested bytes in a dictionary keyed by the content-derived path, which gives the same
/// content-hash naming and de-duplication the real store must provide.
/// </remarks>
internal sealed class InMemoryAssetStore : IAssetStore
{
    private readonly Dictionary<string, byte[]> _assets = [];

    public async Task<string> IngestAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var path = $"assets/{hash}.{fileExtension}";

        _assets[path] = bytes;
        return path;
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return Task.FromResult(_assets.ContainsKey(relativePath));
    }

    public Task<IReadOnlyCollection<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<string>>(_assets.Keys.ToList());
    }
}
