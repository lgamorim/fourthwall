using System.Data.Common;
using Fourthwall.Application;
using Fourthwall.Domain;
using SqlBound;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Stores the editor's canvas node positions in a story's SQLite database and reads them back.
/// </summary>
/// <remarks>
/// Like <see cref="SqliteStoryRepository"/>, this borrows an already-open, already-migrated
/// connection and never owns it — the story-package orchestration owns that lifetime — and reads
/// and writes through SqlBound's source-generated queries, so no SqlBound or ADO.NET type leaks
/// past this layer.
/// <para>
/// Positions live in <c>editor_scene_layout</c>, one of the <c>editor_*</c> tables the game runtime
/// ignores (design doc decision D7). Its foreign key to <c>scenes</c> cascades on delete, so a
/// scene that goes away takes its position with it and this store never prunes.
/// </para>
/// </remarks>
public sealed partial class SqliteSceneLayoutStore : ISceneLayoutStore
{
    private readonly DbConnection _connection;

    /// <summary>
    /// Initializes a store over an open, migrated story database connection.
    /// </summary>
    /// <param name="connection">The connection to read from and write to; not owned by this instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public SqliteSceneLayoutStore(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<SceneId, ScenePosition>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await ReadLayoutAsync(_connection, cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(row => new SceneId(Guid.Parse(row.SceneId)), row => new ScenePosition(row.X, row.Y));
    }

    /// <inheritdoc/>
    public async Task SaveAsync(
        IReadOnlyDictionary<SceneId, ScenePosition> positions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positions);

        // One transaction for the batch, so a position naming a scene the story has not saved
        // rolls back the ones that went before it rather than leaving half a layout behind.
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var (sceneId, position) in positions)
            {
                await UpsertPositionAsync(
                    _connection,
                    transaction,
                    sceneId.Value.ToString(),
                    position.X,
                    position.Y,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException exception)
        {
            // The foreign key to scenes is immediate, so this is a position for a scene that does
            // not exist. The provider's exception must not escape this adapter (CLAUDE.md: provider
            // types stay in Infrastructure), so it becomes the failure the port documents.
            throw new InvalidOperationException(
                "A node position names a scene the story has not saved; no positions were stored.",
                exception);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    [SqlQuery("SELECT scene_id AS SceneId, x AS X, y AS Y FROM editor_scene_layout")]
    private static partial Task<IReadOnlyList<LayoutRow>> ReadLayoutAsync(
        DbConnection connection, CancellationToken cancellationToken);

    [SqlExecute(
        "INSERT INTO editor_scene_layout (scene_id, x, y) VALUES (@sceneId, @x, @y) " +
        "ON CONFLICT(scene_id) DO UPDATE SET x = excluded.x, y = excluded.y")]
    private static partial Task<int> UpsertPositionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sceneId,
        double x,
        double y,
        CancellationToken cancellationToken);
}

/// <summary>A persisted <c>editor_scene_layout</c> row.</summary>
internal sealed record LayoutRow(string SceneId, double X, double Y);
