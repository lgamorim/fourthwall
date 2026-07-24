using System.Data.Common;
using Fourthwall.Application;
using Fourthwall.Domain;
using SqlBound;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Persists a <see cref="Story"/> aggregate into a story's SQLite database and reads it back.
/// </summary>
/// <remarks>
/// The repository borrows an already-open, already-migrated connection; it never opens, closes, or
/// otherwise owns it — the story-package orchestration owns that lifetime. Reads and writes go
/// through SqlBound's source-generated queries, so no SqlBound or ADO.NET type leaks past this layer.
/// <para>
/// A save is a single transaction: the previous scenes and choices are cleared and the whole
/// aggregate is written afresh. The scene and story self-references are deferred to commit (see the
/// initial migration), so a story's legal cycles need no particular insert order.
/// </para>
/// </remarks>
public sealed partial class SqliteStoryRepository : IStoryRepository
{
    private readonly DbConnection _connection;

    /// <summary>
    /// Initializes a repository over an open, migrated story database connection.
    /// </summary>
    /// <param name="connection">The connection to read from and write to; not owned by this instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public SqliteStoryRepository(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Story story, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(story);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await DeleteChoicesAsync(_connection, transaction, cancellationToken).ConfigureAwait(false);
        await DeleteScenesAsync(_connection, transaction, cancellationToken).ConfigureAwait(false);

        foreach (var scene in story.Scenes)
        {
            await InsertSceneAsync(
                _connection,
                transaction,
                IdText(scene.Id),
                scene.Kind.ToString(),
                scene.Text,
                scene.ImagePath,
                scene.FollowUpSceneId is { } followUp ? IdText(followUp) : null,
                scene.Outcome?.Kind.ToString(),
                scene.Outcome?.Label,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var scene in story.Scenes)
        {
            for (var orderIndex = 0; orderIndex < scene.Choices.Count; orderIndex++)
            {
                var choice = scene.Choices[orderIndex];
                await InsertChoiceAsync(
                    _connection,
                    transaction,
                    IdText(scene.Id),
                    orderIndex,
                    choice.Label,
                    IdText(choice.TargetSceneId),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await UpsertStoryAsync(
            _connection,
            transaction,
            story.Title,
            story.StartSceneId is { } start ? IdText(start) : null,
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Story?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var storyRows = await ReadStoryAsync(_connection, cancellationToken).ConfigureAwait(false);
        if (storyRows.Count == 0)
        {
            return null;
        }

        var storyRow = storyRows[0];
        var sceneRows = await ReadScenesAsync(_connection, cancellationToken).ConfigureAwait(false);
        var choiceRows = await ReadChoicesAsync(_connection, cancellationToken).ConfigureAwait(false);

        var story = new Story(storyRow.Title);

        // Every scene first, so that a choice, follow-up, or start scene can reference any of them
        // regardless of the order the rows come back in.
        foreach (var row in sceneRows)
        {
            var scene = story.AddScene(ParseId(row.Id), Enum.Parse<SceneKind>(row.Kind), row.Text, ToOutcome(row));
            if (row.ImagePath is { } imagePath)
            {
                scene.AttachImage(imagePath);
            }
        }

        // Choices arrive ordered by their sibling position, so wiring them in sequence restores it.
        foreach (var row in choiceRows)
        {
            story.WireChoice(ParseId(row.SceneId), row.Label, ParseId(row.TargetSceneId));
        }

        foreach (var row in sceneRows)
        {
            if (row.FollowUpSceneId is { } followUp)
            {
                story.SetFollowUp(ParseId(row.Id), ParseId(followUp));
            }
        }

        if (storyRow.StartSceneId is { } startSceneId)
        {
            story.SetStartScene(ParseId(startSceneId));
        }

        return story;
    }

    private static string IdText(SceneId id) => id.Value.ToString();

    private static SceneId ParseId(string value) => new(Guid.Parse(value));

    private static EndingOutcome? ToOutcome(SceneRow row)
    {
        if (row.OutcomeKind is null)
        {
            return null;
        }

        return Enum.Parse<OutcomeKind>(row.OutcomeKind) switch
        {
            OutcomeKind.Death => EndingOutcome.Death(row.OutcomeLabel),
            OutcomeKind.Victory => EndingOutcome.Victory(row.OutcomeLabel),
            // The 'Other'-has-a-label invariant is a database CHECK; a null here means a corrupt row.
            OutcomeKind.Other => EndingOutcome.Other(
                row.OutcomeLabel ?? throw new InvalidOperationException("An 'Other' ending has no label.")),
            _ => throw new InvalidOperationException($"Unknown outcome kind '{row.OutcomeKind}'."),
        };
    }

    [SqlExecute("DELETE FROM choices")]
    private static partial Task<int> DeleteChoicesAsync(
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    [SqlExecute("DELETE FROM scenes")]
    private static partial Task<int> DeleteScenesAsync(
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    [SqlExecute(
        "INSERT INTO scenes (id, kind, text, image_path, follow_up_scene_id, outcome_kind, outcome_label) " +
        "VALUES (@id, @kind, @text, @imagePath, @followUpSceneId, @outcomeKind, @outcomeLabel)")]
    private static partial Task<int> InsertSceneAsync(
        DbConnection connection,
        DbTransaction transaction,
        string id,
        string kind,
        string text,
        string? imagePath,
        string? followUpSceneId,
        string? outcomeKind,
        string? outcomeLabel,
        CancellationToken cancellationToken);

    [SqlExecute(
        "INSERT INTO choices (scene_id, order_index, label, target_scene_id) " +
        "VALUES (@sceneId, @orderIndex, @label, @targetSceneId)")]
    private static partial Task<int> InsertChoiceAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sceneId,
        int orderIndex,
        string label,
        string targetSceneId,
        CancellationToken cancellationToken);

    [SqlExecute(
        "INSERT INTO stories (id, title, start_scene_id) VALUES (1, @title, @startSceneId) " +
        "ON CONFLICT(id) DO UPDATE SET title = @title, start_scene_id = @startSceneId")]
    private static partial Task<int> UpsertStoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string title,
        string? startSceneId,
        CancellationToken cancellationToken);

    [SqlQuery("SELECT title AS Title, start_scene_id AS StartSceneId FROM stories WHERE id = 1")]
    private static partial Task<IReadOnlyList<StoryRow>> ReadStoryAsync(
        DbConnection connection, CancellationToken cancellationToken);

    [SqlQuery(
        "SELECT id AS Id, kind AS Kind, text AS Text, image_path AS ImagePath, " +
        "follow_up_scene_id AS FollowUpSceneId, outcome_kind AS OutcomeKind, outcome_label AS OutcomeLabel " +
        "FROM scenes")]
    private static partial Task<IReadOnlyList<SceneRow>> ReadScenesAsync(
        DbConnection connection, CancellationToken cancellationToken);

    [SqlQuery(
        "SELECT scene_id AS SceneId, label AS Label, target_scene_id AS TargetSceneId " +
        "FROM choices ORDER BY scene_id, order_index")]
    private static partial Task<IReadOnlyList<ChoiceRow>> ReadChoicesAsync(
        DbConnection connection, CancellationToken cancellationToken);
}

/// <summary>The persisted <c>stories</c> row.</summary>
internal sealed record StoryRow(string Title, string? StartSceneId);

/// <summary>A persisted <c>scenes</c> row; nullable columns map to nullable members.</summary>
internal sealed record SceneRow(
    string Id,
    string Kind,
    string Text,
    string? ImagePath,
    string? FollowUpSceneId,
    string? OutcomeKind,
    string? OutcomeLabel);

/// <summary>A persisted <c>choices</c> row, read in sibling order.</summary>
internal sealed record ChoiceRow(string SceneId, string Label, string TargetSceneId);
