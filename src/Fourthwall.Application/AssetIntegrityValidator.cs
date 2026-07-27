using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Validates a story's images against its asset store (design doc section 4.2 rule 6).
/// </summary>
/// <remarks>
/// Both failure modes are computed from a single <see cref="IAssetStore.ListAsync"/> call and the
/// story's referenced paths, as a set difference: a scene whose image is not among the stored assets
/// is a broken reference, and a stored asset no scene references is an orphan. Each contributes at
/// most one warning, so the number of violations stays bounded regardless of the story's size —
/// though the orphan warning does list every unreferenced path in its message.
/// </remarks>
public sealed class AssetIntegrityValidator : IAssetIntegrityValidator
{
    /// <inheritdoc />
    public async Task<ValidationReport> ValidateAsync(
        Story story, IAssetStore assets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(assets);

        var existing = (await assets.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        var referenced = story.Scenes
            .Select(scene => scene.ImagePath)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var violations = new List<ValidationViolation>();
        AddBrokenReferences(story, existing, violations);
        AddOrphanAssets(existing, referenced, violations);

        return new ValidationReport(violations);
    }

    private static void AddBrokenReferences(
        Story story, IReadOnlySet<string> existing, List<ValidationViolation> violations)
    {
        var broken = story.Scenes
            .Where(scene => scene.ImagePath is not null && !existing.Contains(scene.ImagePath))
            .Select(scene => scene.Id)
            .OrderBy(id => id.Value)
            .ToList();

        if (broken.Count == 0)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.BrokenImageReference,
            ValidationSeverity.Warning,
            $"{broken.Count} scene(s) reference an image that no asset resolves.",
            broken));
    }

    private static void AddOrphanAssets(
        IReadOnlySet<string> existing, IReadOnlySet<string> referenced, List<ValidationViolation> violations)
    {
        var orphans = existing
            .Where(path => !referenced.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (orphans.Count == 0)
        {
            return;
        }

        violations.Add(new ValidationViolation(
            ValidationRule.OrphanAsset,
            ValidationSeverity.Warning,
            $"{orphans.Count} asset(s) are not referenced by any scene: {string.Join(", ", orphans)}.",
            []));
    }
}
