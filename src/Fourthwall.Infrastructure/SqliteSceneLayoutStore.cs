using System.Data.Common;
using Fourthwall.Application;
using Fourthwall.Domain;
using Microsoft.Data.Sqlite;
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
    // SQLITE_CONSTRAINT_FOREIGNKEY: SQLITE_CONSTRAINT (19) in the low byte, cause 3 above it.
    private const int ForeignKeyViolation = 787;

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
        // rolls back the ones that went before it rather than leaving half a layout behind. The
        // transaction begins inside the try: it takes the write lock at once, so a database held
        // by another process fails here, before any statement runs.
        try
        {
            await using var transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

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

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception)
        {
            // The provider's exception must not escape this adapter (CLAUDE.md: provider types stay
            // in Infrastructure), but the reason has to survive the translation: a locked database
            // or a full disk reported as a missing scene sends the creator looking for a story
            // problem that is not there.
            throw new InvalidOperationException(Describe(exception), exception);
        }
    }

    private static string Describe(DbException exception) =>
        // Written for the creator, who reads it after the canvas's own "That scene's place on the
        // map couldn't be saved." The foreign key to scenes is immediate, so a position for a scene
        // the story has not saved fails here rather than at commit; every other cause is the
        // database itself, and the provider's reason (a held file, a full disk) is the useful part.
        exception is SqliteException { SqliteExtendedErrorCode: ForeignKeyViolation }
            ? "The scene isn't in the saved story yet."
            : $"The story file couldn't be written. {exception.Message}";

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
