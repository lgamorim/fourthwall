using Fourthwall.Application;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Pages;

public partial class Home : IDisposable
{
    // Prerendering runs this page twice — once on the server, once when the circuit attaches —
    // and the recent list comes off disk. Persisting it across the handoff means one read per
    // visit instead of two, without giving up prerendering.
    private const string RecentStateKey = "recent-stories";

    private readonly HashSet<string> _unavailable = new(StringComparer.Ordinal);
    private readonly CreateStoryInput _create = new();
    private readonly OpenStoryInput _open = new();
    private IReadOnlyList<RecentStory> _recent = [];
    private string? _error;
    private PersistingComponentStateSubscription _persisting;

    // default!: the framework assigns every [Inject] property before any member of the component
    // runs, so these are never observed null.
    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    [Inject]
    private IRecentStories Recent { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private PersistentComponentState State { get; set; } = default!;

    public void Dispose()
    {
        Workspace.Changed -= OnWorkspaceChanged;
        _persisting.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // The header owns the close action, so the story can be closed while this page is showing
        // it. Follow the workspace rather than only the clicks that happen here.
        Workspace.Changed += OnWorkspaceChanged;
        _persisting = State.RegisterOnPersisting(PersistRecentAsync);

        if (State.TryTakeFromJson<IReadOnlyList<RecentStory>>(RecentStateKey, out var restored))
        {
            _recent = restored ?? [];
            return;
        }

        await RefreshRecentAsync();
    }

    private Task PersistRecentAsync()
    {
        State.PersistAsJson(RecentStateKey, _recent);
        return Task.CompletedTask;
    }

    private async Task CreateAsync()
    {
        // Shape is the form's job — reaching here means a folder and a title were typed. Whether
        // a story can be created there is the workspace's answer.
        _error = null;

        try
        {
            var story = await Workspace.CreateAsync(_create.FolderPath, _create.Title);
            await RememberAsync(_create.FolderPath, story.Title);
            _create.FolderPath = string.Empty;
            _create.Title = string.Empty;
            Navigation.NavigateTo("/story");
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            _error = exception.Message;
        }
    }

    private async Task OpenAsync()
    {
        _error = null;

        if (await TryOpenAsync(_open.FolderPath))
        {
            _open.FolderPath = string.Empty;
        }
    }

    private async Task OpenRecentAsync(RecentStory story)
    {
        _error = null;

        if (await TryOpenAsync(story.FolderPath))
        {
            _unavailable.Remove(story.FolderPath);
        }
        else
        {
            // The folder was remembered but cannot be opened now — moved, deleted, or unreadable.
            // Mark it so the creator can tell which entry is dead and forget it.
            _unavailable.Add(story.FolderPath);
        }
    }

    private async Task<bool> TryOpenAsync(string folderPath)
    {
        try
        {
            var story = await Workspace.OpenAsync(folderPath);
            await RememberAsync(folderPath, story.Title);
            Navigation.NavigateTo("/story");
            return true;
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            _error = exception.Message;
            return false;
        }
    }

    private async Task ForgetAsync(RecentStory story)
    {
        _unavailable.Remove(story.FolderPath);
        await Recent.RemoveAsync(story.FolderPath);
        await RefreshRecentAsync();
    }

    private async Task RememberAsync(string folderPath, string title)
    {
        await Recent.RecordAsync(folderPath, title);
        await RefreshRecentAsync();
    }

    private async Task RefreshRecentAsync() => _recent = await Recent.ListAsync();

    private void OnWorkspaceChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);
}
