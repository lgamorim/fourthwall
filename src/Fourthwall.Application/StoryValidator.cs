using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Validates a story against the structural rules of design doc section 4.2.
/// </summary>
/// <remarks>
/// Rules 1 and 3 are answered from the story itself; rules 2, 4 and 5 need graph traversal and
/// are answered through <see cref="IStoryGraph"/>. Each rule contributes at most one violation,
/// listing every scene at fault, so a report never grows with the size of the story.
/// <para>
/// Rules that cannot be meaningfully evaluated are skipped rather than reported misleadingly:
/// the reachability rules need a start scene, and rule 5 needs the story to contain an ending.
/// In both cases another rule has already reported the underlying problem.
/// </para>
/// </remarks>
public sealed class StoryValidator : IStoryValidator
{
    private readonly IStoryGraphFactory _graphFactory;

    /// <summary>
    /// Initializes a new validator.
    /// </summary>
    /// <param name="graphFactory">Builds the graph used to answer the reachability rules.</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphFactory"/> is <see langword="null"/>.</exception>
    public StoryValidator(IStoryGraphFactory graphFactory)
    {
        ArgumentNullException.ThrowIfNull(graphFactory);
        _graphFactory = graphFactory;
    }

    /// <inheritdoc />
    public ValidationReport Validate(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        var violations = new List<ValidationViolation>();
        var graph = _graphFactory.Create(story);

        CheckStartSceneExists(story, violations);

        if (story.StartSceneId is { } start)
        {
            var reachable = graph.ReachableFrom(start);
            CheckEverySceneIsReachable(story, reachable, violations);
            CheckAnEndingIsReachable(story, reachable, violations);
        }

        CheckOutgoingDegreeMatchesKind(story, violations);
        CheckEverySceneCanReachAnEnding(story, graph, violations);

        return new ValidationReport(violations);
    }

    private static void CheckStartSceneExists(Story story, List<ValidationViolation> violations)
    {
        if (story.StartSceneId is not null)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.SingleStartScene,
            ValidationSeverity.Error,
            "The story has no start scene.",
            []));
    }

    private static void CheckEverySceneIsReachable(
        Story story,
        IReadOnlySet<SceneId> reachable,
        List<ValidationViolation> violations)
    {
        var unreachable = story.Scenes
            .Where(scene => !reachable.Contains(scene.Id))
            .Select(scene => scene.Id)
            .ToList();

        if (unreachable.Count == 0)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.AllScenesReachable,
            ValidationSeverity.Error,
            $"{unreachable.Count} scene(s) cannot be reached from the start scene.",
            unreachable));
    }

    private static void CheckAnEndingIsReachable(
        Story story,
        IReadOnlySet<SceneId> reachable,
        List<ValidationViolation> violations)
    {
        var anyEndingReachable = story.Scenes
            .Any(scene => scene.Kind == SceneKind.Ending && reachable.Contains(scene.Id));

        if (anyEndingReachable)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.EndingReachable,
            ValidationSeverity.Error,
            "No ending can be reached from the start scene.",
            []));
    }

    private static void CheckOutgoingDegreeMatchesKind(Story story, List<ValidationViolation> violations)
    {
        // Ending scenes are not checked here: Scene already makes "an ending has no outgoing
        // transitions" a hard invariant, so a violating scene cannot be constructed and the
        // branch would be untestable dead code.
        var offending = story.Scenes
            .Where(HasWrongDegree)
            .Select(scene => scene.Id)
            .ToList();

        if (offending.Count == 0)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.OutgoingDegreeMatchesKind,
            ValidationSeverity.Error,
            $"{offending.Count} scene(s) have a number of outgoing transitions that does not match their kind.",
            offending));

        static bool HasWrongDegree(Scene scene) => scene.Kind switch
        {
            SceneKind.Choice => scene.OutgoingSceneIds.Count() < 2,
            SceneKind.Linear => scene.OutgoingSceneIds.Count() != 1,
            _ => false,
        };
    }

    private static void CheckEverySceneCanReachAnEnding(
        Story story,
        IStoryGraph graph,
        List<ValidationViolation> violations)
    {
        var endings = story.Scenes
            .Where(scene => scene.Kind == SceneKind.Ending)
            .Select(scene => scene.Id)
            .ToHashSet();

        if (endings.Count == 0)
        {
            return;
        }

        var canReachAnEnding = graph.ScenesThatCanReachAny(endings);
        var trapped = story.Scenes
            .Where(scene => !canReachAnEnding.Contains(scene.Id))
            .Select(scene => scene.Id)
            .ToList();

        if (trapped.Count == 0)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.EverySceneCanReachEnding,
            ValidationSeverity.Warning,
            $"{trapped.Count} scene(s) cannot reach any ending, so a reader can become trapped.",
            trapped));
    }
}
