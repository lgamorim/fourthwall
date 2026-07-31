using Fourthwall.Application;

using Microsoft.AspNetCore.Components;

namespace Fourthwall.Web.Components.Layout;

public partial class MainLayout : IDisposable
{
    // default!: the framework assigns every [Inject] property before any member of the component
    // runs, so this is never observed null.
    [Inject]
    private IStoryWorkspace Workspace { get; set; } = default!;

    public void Dispose()
    {
        // The workspace outlives every circuit, so a header that stays subscribed after its
        // component is gone would be told to render after disposal.
        Workspace.Changed -= OnWorkspaceChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Workspace.Changed += OnWorkspaceChanged;

    private Task CloseAsync() => Workspace.CloseAsync();

    private void OnWorkspaceChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);
}
