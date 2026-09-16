using Fourthwall.Application;
using Fourthwall.Domain;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Editor;

public partial class ValidationPanel : IDisposable
{
    private ValidationReport? _report;
    private string? _failure;
    private bool _validating;

    // Bumped whenever the story changes. A validation that started before the change is stale by
    // the time it finishes, however quickly it finished.
    private int _storyRevision;

    /// <summary>
    /// Raised when a creator picks one of the scenes a violation names.
    /// </summary>
    [Parameter]
    public EventCallback<SceneId?> OnSceneSelected { get; set; }

    /// <summary>
    /// Raised with the report the panel now shows: the new one when a validation completes, and
    /// <see langword="null"/> when it stops showing one — a validation starting again, or the story
    /// changing. The canvas marks the scenes it blames, so the map never shows what the panel does not.
    /// </summary>
    [Parameter]
    public EventCallback<ValidationReport?> OnReportChanged { get; set; }

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
        await ShowReportAsync(null);

        if (Workspace.Current is not { } story || Workspace.Assets is not { } assets)
        {
            return;
        }

        var revision = _storyRevision;
        _validating = true;

        try
        {
            var report = await Validation.ValidateAsync(story, assets);

            // The story may have been edited while this ran. Installing the result now would show
            // a report of the story as it used to be — the quiet kind of wrong this panel exists
            // to avoid.
            if (revision == _storyRevision)
            {
                await ShowReportAsync(report);
            }
        }
        catch (Exception exception) when (UserFacingFailures.Includes(exception))
        {
            // The asset half reads the story folder, which can be gone or unreadable.
            if (revision == _storyRevision)
            {
                _failure = exception.Message;
            }
        }
        finally
        {
            _validating = false;
        }
    }

    private Task SelectAsync(SceneId sceneId) => OnSceneSelected.InvokeAsync(sceneId);

    // Tells the page only when what is shown changes, so an edit with no report up costs nothing.
    private async Task ShowReportAsync(ValidationReport? report)
    {
        if (ReferenceEquals(report, _report))
        {
            return;
        }

        _report = report;
        await OnReportChanged.InvokeAsync(report);
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e) => InvokeAsync(async () =>
    {
        // The story has been edited or replaced: a report describing what it used to be would
        // quietly mislead, so it goes rather than lingering as though it still applied. The
        // revision bump also disowns any validation still in flight.
        _storyRevision++;
        _failure = null;
        await ShowReportAsync(null);
        StateHasChanged();
    });
}
