using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Fourthwall.Web.Components.Editor;

public partial class SceneImage
{
    /// <summary>
    /// The largest image the editor accepts. Blazor's own read limit defaults to about 500 KB,
    /// which no real scene art fits inside, and exceeding a limit throws rather than reporting —
    /// so the size is checked before the stream is opened.
    /// </summary>
    public const long MaximumBytes = 10L * 1024 * 1024;

    private SceneId _shownScene;
    private string? _error;

    // default!: required parameters are assigned by the framework before any member of the
    // component runs, so this is never observed null.
    [Parameter]
    [EditorRequired]
    public Scene Scene { get; set; } = default!;

    /// <summary>
    /// Raised after a mutation the story should be saved for.
    /// </summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    // default!: assigned by the framework before any member runs.
    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_shownScene == Scene.Id)
        {
            return;
        }

        // A different scene is being edited: an error about the last scene's file is not about
        // this one.
        _shownScene = Scene.Id;
        _error = null;
    }

    private async Task AttachAsync(InputFileChangeEventArgs e)
    {
        _error = null;

        var file = e.File;
        if (!ImageTypes.IsAccepted(file.Name))
        {
            _error = $"Choose an image file ({string.Join(", ", ImageTypes.Extensions)}).";
            return;
        }

        if (file.Size > MaximumBytes)
        {
            _error = $"That image is larger than {MaximumBytes / (1024 * 1024)} MB.";
            return;
        }

        if (Workspace.Assets is not { } assets)
        {
            // The story can be closed between the file being chosen and this callback arriving.
            return;
        }

        try
        {
            var content = file.OpenReadStream(MaximumBytes);
            await using (content.ConfigureAwait(false))
            {
                // Replacing an image leaves the previous file in place. Content-hashed names may be
                // shared by other scenes, so deleting here could break them; an unreferenced asset
                // is reported as a warning by asset-integrity validation instead.
                var path = await assets.IngestAsync(content, ImageTypes.ExtensionOf(file.Name));
                Scene.AttachImage(path);
            }
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            // Ingesting writes into the story folder, which can be read-only, full, or gone.
            _error = exception.Message;
            return;
        }

        await OnChanged.InvokeAsync();
    }

    private async Task ClearAsync()
    {
        _error = null;
        Scene.ClearImage();
        await OnChanged.InvokeAsync();
    }
}
