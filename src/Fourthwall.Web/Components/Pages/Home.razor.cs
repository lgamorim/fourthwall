using Fourthwall.Application;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Pages;

public partial class Home : IDisposable
{
    private readonly HashSet<string> _unavailable = new(StringComparer.Ordinal);
    private IReadOnlyList<RecentStory> _recent = [];
    private string _createFolder = string.Empty;
    private string _createTitle = string.Empty;
    private string _openFolder = string.Empty;
    private string? _error;

    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    [Inject]
    private IRecentStories Recent { get; set; } = default!;

    public void Dispose()
    {
        Workspace.Changed -= OnWorkspaceChanged;
        GC.SuppressFinalize(this);
    }

    protected override Task OnInitializedAsync()
    {
        // The header owns the close action, so the story can be closed while this page is showing
        // it. Follow the workspace rather than only the clicks that happen here.
        Workspace.Changed += OnWorkspaceChanged;
        return RefreshRecentAsync();
    }

    // Everything the creator can get wrong about a folder path arrives as one of these, and all of
    // them belong on the page rather than in the error boundary.
    private static bool IsUserFacing(Exception exception) =>
        exception is InvalidOperationException or IOException or UnauthorizedAccessException;

    private async Task CreateAsync()
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_createFolder))
        {
            _error = "Enter the folder to create the story in.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_createTitle))
        {
            _error = "Enter a title for the story.";
            return;
        }

        try
        {
            var story = await Workspace.CreateAsync(_createFolder, _createTitle);
            await RememberAsync(_createFolder, story.Title);
            _createFolder = string.Empty;
            _createTitle = string.Empty;
        }
        catch (Exception exception) when (IsUserFacing(exception))
        {
            _error = exception.Message;
        }
    }

    private async Task OpenAsync()
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_openFolder))
        {
            _error = "Enter the folder holding the story.";
            return;
        }

        if (await TryOpenAsync(_openFolder))
        {
            _openFolder = string.Empty;
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
            return true;
        }
        catch (Exception exception) when (IsUserFacing(exception))
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
