using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fourthwall.Web.Components.Pages;

public partial class StoryEditor : IDisposable
{
    private readonly RenderFragment _dock;
    private SceneId? _selectedSceneId;
    private string _title = string.Empty;
    private string? _error;

    public StoryEditor()
    {
        // SectionOutlet keys what it renders by the RenderFragment it is handed, so a different
        // delegate tears down and recreates every component in the dock. Markup written inline
        // under <SectionContent> compiles to a new lambda on every render of this page, which wiped
        // the validation report, the scene draft, and any pending prompt whenever a scene was
        // selected. One delegate, created once and reading the page's current state each time it
        // runs, keeps the key stable; the outlet still re-renders it, so the dock sees every change.
        _dock = RenderDock;
    }

    // default!: the framework assigns every [Inject] property before any member of the component
    // runs, so these are never observed null.
    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

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

    private void RenderDock(RenderTreeBuilder builder)
    {
        // The story can close on another circuit between this page's render and the outlet's.
        if (Workspace.Current is not { } story)
        {
            return;
        }

        builder.OpenComponent<EditorDock>(0);
        builder.AddComponentParameter(1, nameof(EditorDock.Story), story);
        builder.AddComponentParameter(2, nameof(EditorDock.SelectedSceneId), _selectedSceneId);
        builder.AddComponentParameter(
            3, nameof(EditorDock.OnSceneSelected), EventCallback.Factory.Create<SceneId?>(this, OnSceneSelected));
        builder.AddComponentParameter(4, nameof(EditorDock.OnChanged), EventCallback.Factory.Create(this, SaveAsync));
        builder.CloseComponent();
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
