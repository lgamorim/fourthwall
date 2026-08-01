using Fourthwall.Domain;

namespace Fourthwall.Application;

/// <summary>
/// Runs the structural and asset-integrity validators and returns their findings as one report.
/// </summary>
public sealed class StoryValidation : IStoryValidation
{
    private readonly IStoryValidator _structure;
    private readonly IAssetIntegrityValidator _assets;

    /// <summary>
    /// Initializes a validation over the two halves of the rule set.
    /// </summary>
    /// <param name="structure">Validates the story graph (rules 1 to 5).</param>
    /// <param name="assets">Validates the story's images (rule 6).</param>
    /// <exception cref="ArgumentNullException">Either validator is <see langword="null"/>.</exception>
    public StoryValidation(IStoryValidator structure, IAssetIntegrityValidator assets)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(assets);

        _structure = structure;
        _assets = assets;
    }

    /// <inheritdoc/>
    public async Task<ValidationReport> ValidateAsync(
        Story story, IAssetStore assets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(assets);
        cancellationToken.ThrowIfCancellationRequested();

        // Structure first, so the report reads in the order the design states the rules.
        var structural = _structure.Validate(story);
        var integrity = await _assets.ValidateAsync(story, assets, cancellationToken).ConfigureAwait(false);

        return new ValidationReport([.. structural.Violations, .. integrity.Violations]);
    }
}
