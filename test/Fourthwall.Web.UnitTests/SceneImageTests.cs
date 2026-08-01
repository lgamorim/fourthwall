using Fourthwall.Application;
using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Fourthwall.Web.UnitTests;

public class SceneImageTests : BunitContext
{
    private static readonly byte[] Pixels = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
    private readonly FakeStoryWorkspace _workspace = new();

    public SceneImageTests()
    {
        Services.AddSingleton<IStoryWorkspace>(_workspace);
    }

    [Fact]
    public async Task Should_AttachTheImage_When_AFileIsChosen()
    {
        // Arrange
        var scene = await OpenSceneAsync();
        var changed = 0;
        var cut = RenderFor(scene, () => changed++);

        // Act
        Upload(cut, "storm.png");

        // Assert — ingested under a content-hashed name, and recorded on the scene.
        Assert.Equal(1, _workspace.AssetStore.IngestCount);
        Assert.StartsWith("assets/", scene.ImagePath);
        Assert.EndsWith(".png", scene.ImagePath);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Should_ShowThePreview_When_TheSceneHasAnImage()
    {
        // Arrange
        var scene = await OpenSceneAsync();
        var cut = RenderFor(scene);

        // Act
        Upload(cut, "storm.png");

        // Assert — served through the asset endpoint, not from wwwroot.
        var source = cut.Find(".scene-image-preview").GetAttribute("src");
        Assert.Equal($"/story-asset/{scene.ImagePath}", source);
    }

    [Fact]
    public async Task Should_ShowNoPreview_When_TheSceneHasNoImage()
    {
        // Arrange
        var scene = await OpenSceneAsync();

        // Act
        var cut = RenderFor(scene);

        // Assert
        Assert.Empty(cut.FindAll(".scene-image-preview"));
    }

    [Fact]
    public async Task Should_RefuseTheFile_When_ItIsNotAnAcceptedImage()
    {
        // Arrange
        var scene = await OpenSceneAsync();
        var cut = RenderFor(scene);

        // Act
        Upload(cut, "notes.txt");

        // Assert
        Assert.Null(scene.ImagePath);
        Assert.Equal(0, _workspace.AssetStore.IngestCount);
        Assert.NotNull(cut.Find(".scene-image-error"));
    }

    [Fact]
    public async Task Should_RefuseTheFile_When_ItIsLargerThanTheLimit()
    {
        // Arrange — Blazor's default read limit is far below scene-art size, and exceeding the cap
        // throws rather than reporting, so the component has to check first.
        var scene = await OpenSceneAsync();
        var cut = RenderFor(scene);
        var oversized = new byte[SceneImage.MaximumBytes + 1];

        // Act
        Upload(cut, "huge.png", oversized);

        // Assert
        Assert.Null(scene.ImagePath);
        Assert.Equal(0, _workspace.AssetStore.IngestCount);
        Assert.NotNull(cut.Find(".scene-image-error"));
    }

    [Fact]
    public async Task Should_ClearTheReference_When_ClearIsChosen()
    {
        // Arrange
        var scene = await OpenSceneAsync();
        var cut = RenderFor(scene);
        Upload(cut, "storm.png");
        var stored = scene.ImagePath;

        // Act
        cut.Find(".scene-image-clear").Click();

        // Assert — the reference goes; the file stays, since other scenes may share it.
        Assert.Null(scene.ImagePath);
        Assert.True(await _workspace.AssetStore.ExistsAsync(stored!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_OfferNoClear_When_TheSceneHasNoImage()
    {
        // Arrange
        var scene = await OpenSceneAsync();

        // Act
        var cut = RenderFor(scene);

        // Assert
        Assert.Empty(cut.FindAll(".scene-image-clear"));
    }

    [Fact]
    public async Task Should_ClearTheError_When_ADifferentSceneIsSelected()
    {
        // Arrange — the reset every stateful editor component in this dock now does.
        var scene = await OpenSceneAsync();
        var other = _workspace.Current!.AddScene(SceneKind.Linear, "Below deck");
        var cut = RenderFor(scene);
        Upload(cut, "notes.txt");

        // Act
        cut.Render(parameters => parameters.Add(p => p.Scene, other));

        // Assert
        Assert.Empty(cut.FindAll(".scene-image-error"));
    }

    private static void Upload(
        IRenderedComponent<SceneImage> cut, string fileName, byte[]? content = null) =>
        cut.FindComponent<InputFile>()
            .UploadFiles(InputFileContent.CreateFromBinary(content ?? Pixels, fileName));

    private async Task<Scene> OpenSceneAsync()
    {
        var story = await _workspace.CreateAsync(
            @"C:\stories\wreck", "The Wreck", TestContext.Current.CancellationToken);
        return story.AddScene(SceneKind.Linear, "A storm gathers");
    }

    private IRenderedComponent<SceneImage> RenderFor(Scene scene, Action? onChanged = null) =>
        Render<SceneImage>(parameters => parameters
            .Add(p => p.Scene, scene)
            .Add(p => p.OnChanged, () => onChanged?.Invoke()));
}
