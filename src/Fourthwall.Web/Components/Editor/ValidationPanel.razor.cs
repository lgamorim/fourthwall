using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class ValidationPanel : IDisposable
{
    private ValidationReport? _report;
    private string? _failure;

    /// <summary>
    /// Raised when a creator picks one of the scenes a violation names.
    /// </summary>
    [Parameter]
    public EventCallback<SceneId?> OnSceneSelected { get; set; }

    // default!: the framework assigns every [Inject] property before any member of the component
    // runs, so these are never observed null.
    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    [Inject]
    private IStoryValidation Validation { get; set; } = default!;

    public void Dispose()
    {
        Workspace.Changed -= OnWorkspaceChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Workspace.Changed += OnWorkspaceChanged;

    private static string SeverityClass(ValidationViolation violation) =>
        violation.Severity == ValidationSeverity.Error ? "validation-error" : "validation-warning";

    private async Task ValidateAsync()
    {
        _failure = null;
        _report = null;

        if (Workspace.Current is not { } story || Workspace.Assets is not { } assets)
        {
            return;
        }

        try
        {
            _report = await Validation.ValidateAsync(story, assets);
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            // The asset half reads the story folder, which can be gone or unreadable.
            _failure = exception.Message;
        }
    }

    private Task SelectAsync(SceneId sceneId) => OnSceneSelected.InvokeAsync(sceneId);

    private void OnWorkspaceChanged(object? sender, EventArgs e) => InvokeAsync(() =>
    {
        // The story has been edited or replaced: a report describing what it used to be would
        // quietly mislead, so it goes rather than lingering as though it still applied.
        _report = null;
        _failure = null;
        StateHasChanged();
    });
}
