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
    /// Adds the story graph, validation, asset-integrity, workspace, and recent-story services.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="recentStoriesPath">
    /// The file the recently opened stories are remembered in. The caller chooses the location, so
    /// this extension stays free of assumptions about the host it runs on.
    /// </param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="recentStoriesPath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="recentStoriesPath"/> is blank.</exception>
    public static IServiceCollection AddFourthwall(this IServiceCollection services, string recentStoriesPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(recentStoriesPath);

        // All three are stateless, so a single instance serves every circuit.
        services.AddSingleton<IStoryGraphFactory, Graph1xStoryGraphFactory>();
        services.AddSingleton<IStoryValidator, StoryValidator>();
        services.AddSingleton<IAssetIntegrityValidator, AssetIntegrityValidator>();
        services.AddSingleton<IStoryValidation, StoryValidation>();

        // The workspace is stateful and deliberately shared: one story is open per application
        // instance, so two browser tabs are two views of it rather than two sessions. The container
        // disposes it at shutdown, which closes the story folder.
        services.AddSingleton<IStoryWorkspace, StoryPackageWorkspace>();
        services.AddSingleton<IRecentStories>(_ => new JsonRecentStoriesStore(recentStoriesPath));

        return services;
    }
}
