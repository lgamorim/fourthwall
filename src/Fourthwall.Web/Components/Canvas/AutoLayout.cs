using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// Places scenes that have no saved position, so a story never renders with nodes stacked at the
/// origin. Depth from the start scene picks the column; scenes without a start, or unreachable
/// from it, land in a trailing column.
/// </summary>
public static class AutoLayout
{
    private const double OriginX = 40;
    private const double OriginY = 40;
    private const double ColumnGap = CanvasGeometry.NodeWidth + 60;
    private const double RowGap = CanvasGeometry.NodeHeight + 40;

    /// <summary>
    /// Computes a position for every scene in the story: a saved position passes through
    /// untouched, and every other scene is placed by depth-from-start column and reading order.
    /// </summary>
    /// <param name="story">The story to lay out.</param>
    /// <param name="graph">A graph over <paramref name="story"/>, used to find each scene's depth from the start.</param>
    /// <param name="saved">Positions already saved for this story; a story new to the canvas passes an empty dictionary.</param>
    /// <returns>A position for every scene in <paramref name="story"/>.</returns>
    public static IReadOnlyDictionary<SceneId, ScenePosition> Place(
        Story story, IStoryGraph graph, IReadOnlyDictionary<SceneId, ScenePosition> saved)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(saved);

        var depths = story.StartSceneId is { } start
            ? graph.DepthFrom(start)
            : new Dictionary<SceneId, int>();

        var trailingColumn = depths.Count > 0 ? depths.Values.Max() + 1 : 0;

        var positions = new Dictionary<SceneId, ScenePosition>();
        var nextRowByColumn = new Dictionary<int, int>();

        foreach (var scene in Scenes.Ordered(story))
        {
            if (saved.TryGetValue(scene.Id, out var savedPosition))
            {
                positions[scene.Id] = savedPosition;
                continue;
            }

            var column = depths.TryGetValue(scene.Id, out var depth) ? depth : trailingColumn;
            var row = nextRowByColumn.GetValueOrDefault(column);
            nextRowByColumn[column] = row + 1;

            positions[scene.Id] = new ScenePosition(OriginX + (column * ColumnGap), OriginY + (row * RowGap));
        }

        return positions;
    }
}
