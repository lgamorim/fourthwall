using Fourthwall.Application;
using Fourthwall.Web.Composition;

using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class FourthwallServiceCollectionExtensionsTests
{
    [Fact]
    public void Should_ThrowArgumentNullException_When_ServiceCollectionIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => FourthwallServiceCollectionExtensions.AddFourthwall(null!));
    }

    [Fact]
    public void Should_ReturnSameCollection_When_FourthwallIsAdded()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddFourthwall();

        // Assert — the documented contract is that calls can be chained.
        Assert.Same(services, result);
    }

    [Fact]
    public void Should_ResolveStoryGraphFactory_When_FourthwallIsAdded()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var factory = provider.GetService<IStoryGraphFactory>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Should_ResolveStoryValidator_When_FourthwallIsAdded()
    {
        // Arrange — the validator depends on IStoryGraphFactory, so resolving it
        // also proves that dependency is registered.
        using var provider = BuildProvider();

        // Act
        var validator = provider.GetService<IStoryValidator>();

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public void Should_ResolveAssetIntegrityValidator_When_FourthwallIsAdded()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var validator = provider.GetService<IAssetIntegrityValidator>();

        // Assert
        Assert.NotNull(validator);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddFourthwall();
        return services.BuildServiceProvider();
    }
}
