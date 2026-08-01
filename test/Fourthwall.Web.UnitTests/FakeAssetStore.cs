using System.Security.Cryptography;

using Fourthwall.Application;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// An in-memory <see cref="IAssetStore"/> keeping the real store's content-hashed naming, so paths
/// look like the ones a scene actually records.
/// </summary>
public sealed class FakeAssetStore : IAssetStore
{
    private readonly Dictionary<string, byte[]> _assets = [];

    public int IngestCount { get; private set; }

    /// <summary>
    /// When set, the next ingest throws this instead of succeeding, and the failure is cleared.
    /// </summary>
    public Exception? FailNextIngest { get; set; }

    public async Task<string> IngestAsync(
        Stream content, string fileExtension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        if (FailNextIngest is not null)
        {
            var failure = FailNextIngest;
            FailNextIngest = null;
            throw failure;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var path = $"assets/{Convert.ToHexStringLower(SHA256.HashData(bytes))}.{fileExtension.ToLowerInvariant()}";
        _assets[path] = bytes;
        IngestCount++;
        return path;
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Task.FromResult(_assets.ContainsKey(relativePath));
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return Task.FromResult<Stream?>(
            _assets.TryGetValue(relativePath, out var bytes) ? new MemoryStream(bytes) : null);
    }

    public Task<IReadOnlyCollection<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<string>>(_assets.Keys.ToList());
}
