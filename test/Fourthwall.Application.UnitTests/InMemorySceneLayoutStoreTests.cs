using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

public sealed class InMemorySceneLayoutStoreTests
{
    [Fact]
    public async Task Should_ReturnEmpty_When_NothingSaved()
    {
        var store = new InMemorySceneLayoutStore();

        var positions = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(positions);
    }

    [Fact]
    public async Task Should_OverwritePosition_When_SavedAgainForSameScene()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemorySceneLayoutStore();
        var sceneId = SceneId.New();
        await store.SaveAsync(new Dictionary<SceneId, ScenePosition> { [sceneId] = new(1, 2) }, cancellationToken);

        await store.SaveAsync(new Dictionary<SceneId, ScenePosition> { [sceneId] = new(30, 40) }, cancellationToken);

        var positions = await store.LoadAsync(cancellationToken);
        Assert.Equal(new ScenePosition(30, 40), positions[sceneId]);
    }
}
