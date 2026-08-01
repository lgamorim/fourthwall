using Fourthwall.Application;
using Fourthwall.Domain;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// An <see cref="IStoryValidation"/> that returns whatever report a test hands it, so panel
/// behaviour can be exercised without building a story that provokes each rule.
/// </summary>
public sealed class FakeStoryValidation : IStoryValidation
{
    public ValidationReport Report { get; set; } = new([]);

    /// <summary>
    /// When set, the next validation throws this instead of returning, and the failure is cleared.
    /// </summary>
    public Exception? FailNext { get; set; }

    public Task<ValidationReport> ValidateAsync(
        Story story, IAssetStore assets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(assets);

        if (FailNext is not null)
        {
            var failure = FailNext;
            FailNext = null;
            throw failure;
        }

        return Task.FromResult(Report);
    }
}
