using Fourthwall.Application;
using Fourthwall.Web.Composition;

using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class FourthwallServiceCollectionExtensionsTests
{
    private static readonly string RecentStoriesPath =
        Path.Combine(Path.GetTempPath(), "fourthwall-tests", "recent-stories.json");

    [Fact]
    public void Should_ThrowArgumentNullException_When_ServiceCollectionIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => FourthwallServiceCollectionExtensions.AddFourthwall(null!, RecentStoriesPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_RecentStoriesPathIsBlank(string path)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddFourthwall(path));
    }

    [Fact]
    public void Should_ReturnSameCollection_When_FourthwallIsAdded()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddFourthwall(RecentStoriesPath);

        // Assert — the documented contract is that calls can be chained.
        Assert.Same(services, result);
    }

    [Fact]
    public async Task Should_ResolveStoryGraphFactory_When_FourthwallIsAdded()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        var factory = provider.GetService<IStoryGraphFactory>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public async Task Should_ResolveStoryValidator_When_FourthwallIsAdded()
    {
        // Arrange — the validator depends on IStoryGraphFactory, so resolving it
        // also proves that dependency is registered.
        await using var provider = BuildProvider();

        // Act
        var validator = provider.GetService<IStoryValidator>();

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public async Task Should_ResolveAssetIntegrityValidator_When_FourthwallIsAdded()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        var validator = provider.GetService<IAssetIntegrityValidator>();

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public async Task Should_ResolveStoryWorkspace_When_FourthwallIsAdded()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        var workspace = provider.GetService<IStoryWorkspace>();

        // Assert
        Assert.NotNull(workspace);
    }

    [Fact]
    public async Task Should_ResolveRecentStories_When_FourthwallIsAdded()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        var recent = provider.GetService<IRecentStories>();

        // Assert
        Assert.NotNull(recent);
    }

    [Fact]
    public async Task Should_ShareOneWorkspace_When_ResolvedTwice()
    {
        // Arrange — one story is open per application instance, not per circuit.
        await using var provider = BuildProvider();

        // Act
        var first = provider.GetRequiredService<IStoryWorkspace>();
        var second = provider.GetRequiredService<IStoryWorkspace>();

        // Assert
        Assert.Same(first, second);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddFourthwall(RecentStoriesPath);
        return services.BuildServiceProvider();
    }
}
