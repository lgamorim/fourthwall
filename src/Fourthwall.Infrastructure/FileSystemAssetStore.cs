using System.Security.Cryptography;
using Fourthwall.Application;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Stores a story's scene images as files under its <c>assets/</c> folder, named by content hash.
/// </summary>
/// <remarks>
/// This is the file-backed <see cref="IAssetStore"/>: it copies an image in under a
/// <c>&lt;sha256&gt;.&lt;ext&gt;</c> name (design doc decision D5), so identical images collapse to a
/// single file and a changed image never reuses an old name. Story-relative paths are always
/// <c>/</c>-separated and resolved strictly inside the story folder, so a hand-edited path
/// (design doc D7) can never reach a file outside it.
/// </remarks>
public sealed class FileSystemAssetStore : IAssetStore
{
    private const string AssetsFolderName = "assets";
    private readonly string _storyFolder;
    private readonly string _assetsFolder;

    /// <summary>
    /// Initializes a store over the given story folder; assets live in its <c>assets/</c> subfolder.
    /// </summary>
    /// <param name="storyFolder">The path to the story folder that holds <c>assets/</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="storyFolder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="storyFolder"/> is blank.</exception>
    public FileSystemAssetStore(string storyFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyFolder);
        _storyFolder = Path.GetFullPath(storyFolder);
        _assetsFolder = Path.Combine(_storyFolder, AssetsFolderName);
    }

    /// <inheritdoc/>
    public async Task<string> IngestAsync(
        Stream content, string fileExtension, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);
        EnsureExtensionIsSafe(fileExtension);

        Directory.CreateDirectory(_assetsFolder);

        // Stream through SHA-256 into a temp file, then publish under the hashed name. The move is
        // atomic, so a half-written asset is never visible to ExistsAsync/ListAsync, and the content
        // is never fully buffered in memory.
        var tempPath = Path.Combine(_assetsFolder, $"{Guid.NewGuid():N}.tmp");
        try
        {
            string hash;
            var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write);
            await using (fileStream.ConfigureAwait(false))
            using (var sha256 = SHA256.Create())
            {
                var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);
                await using (cryptoStream.ConfigureAwait(false))
                {
                    await content.CopyToAsync(cryptoStream, cancellationToken).ConfigureAwait(false);
                    await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
                    hash = Convert.ToHexStringLower(sha256.Hash!);
                }
            }

            var fileName = $"{hash}.{fileExtension.ToLowerInvariant()}";
            var finalPath = Path.Combine(_assetsFolder, fileName);

            // Content under a hashed name is identical by construction, so an existing destination —
            // whether from an earlier ingest or one that raced this call between a check and the move
            // — is a successful dedupe, not a conflict. Attempt the move and treat that collision as
            // success, discarding the incoming temp; this stays idempotent under concurrency.
            try
            {
                File.Move(tempPath, finalPath);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                File.Delete(tempPath);
            }

            return $"{AssetsFolderName}/{fileName}";
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return Task.FromResult(TryResolve(relativePath, out var fullPath) && File.Exists(fullPath));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_assetsFolder))
        {
            return Task.FromResult<IReadOnlyCollection<string>>([]);
        }

        var paths = Directory.EnumerateFiles(_assetsFolder)
            .Where(file => !file.EndsWith(".tmp", StringComparison.Ordinal))
            .Select(file => $"{AssetsFolderName}/{Path.GetFileName(file)}")
            .ToList();

        return Task.FromResult<IReadOnlyCollection<string>>(paths);
    }

    private static void EnsureExtensionIsSafe(string fileExtension)
    {
        if (fileExtension.Contains('.') || fileExtension.Contains('/') || fileExtension.Contains('\\'))
        {
            throw new ArgumentException(
                "A file extension must not contain a dot or a path separator.", nameof(fileExtension));
        }
    }

    // Resolves a story-relative path to an absolute path, refusing anything that escapes the story
    // folder. Returns false rather than throwing so a bad reference is reported as "does not exist".
    private bool TryResolve(string relativePath, out string fullPath)
    {
        var combined = Path.GetFullPath(Path.Combine(_storyFolder, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = _storyFolder + Path.DirectorySeparatorChar;

        // Ordinal (case-sensitive) is deliberate, even on a case-insensitive filesystem: paths this
        // store produces always match the folder's casing exactly, so legitimate lookups resolve,
        // while a contrived escape-and-return with altered casing fails the check and is reported
        // absent. This is a security check that must fail closed — do not relax it to
        // OrdinalIgnoreCase.
        if (combined.StartsWith(root, StringComparison.Ordinal))
        {
            fullPath = combined;
            return true;
        }

        fullPath = string.Empty;
        return false;
    }
}
