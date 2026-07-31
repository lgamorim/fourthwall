using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Pages;

public partial class StoryEditor : IDisposable
{
    private SceneId? _selectedSceneId;
    private string _title = string.Empty;
    private string? _error;

    // default!: the framework assigns every [Inject] property before any member of the component
    // runs, so these are never observed null.
    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private Scene? SelectedScene =>
        _selectedSceneId is { } id ? Workspace.Current?.FindScene(id) : null;

    public void Dispose()
    {
        Workspace.Changed -= OnWorkspaceChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        Workspace.Changed += OnWorkspaceChanged;

        if (Workspace.Current is null)
        {
            // Nothing to edit — the picker is where a story gets opened.
            Navigation.NavigateTo("/");
            return;
        }

        _title = Workspace.Current.Title;
    }

    private void OnSceneSelected(SceneId? sceneId) => _selectedSceneId = sceneId;

    private async Task RenameAsync()
    {
        _error = null;

        // The commit can arrive for a render that is already stale — the header in this tab, or a
        // second tab sharing the workspace, may have closed the story first.
        if (Workspace.Current is not { } story)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_title))
        {
            // Story.Rename rejects a blank title; say so rather than let it throw.
            _error = "A story needs a title.";
            _title = story.Title;
            return;
        }

        story.Rename(_title);
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        _error = null;

        try
        {
            await Workspace.SaveAsync();
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            _error = exception.Message;
        }
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e) => InvokeAsync(() =>
    {
        if (Workspace.Current is null)
        {
            // The header can close the story while this page is showing it.
            Navigation.NavigateTo("/");
            return;
        }

        StateHasChanged();
    });
}
