using Fourthwall.Application;
using Fourthwall.Infrastructure;

namespace Fourthwall.Web.Composition;

/// <summary>
/// Registers the Fourthwall services in the application's dependency injection
/// container. The Blazor app is the composition root, so this is the one place
/// Infrastructure types are named.
/// </summary>
public static class FourthwallServiceCollectionExtensions
{
    /// <summary>
    /// Adds the story graph, validation, and asset-integrity services.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddFourthwall(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // All three are stateless, so a single instance serves every circuit.
        services.AddSingleton<IStoryGraphFactory, Graph1xStoryGraphFactory>();
        services.AddSingleton<IStoryValidator, StoryValidator>();
        services.AddSingleton<IAssetIntegrityValidator, AssetIntegrityValidator>();

        return services;
    }
}
