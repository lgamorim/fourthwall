using Fourthwall.Application;
using Fourthwall.Web.Components;
using Fourthwall.Web.Composition;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Recently opened stories are editor state, not story data, so they live outside every story
// folder — under the user's application data rather than anywhere the tool ships from.
var recentStoriesPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Fourthwall",
    "recent-stories.json");

builder.Services.AddFourthwall(recentStoriesPath);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Scene images live in the open story's folder, not under wwwroot, so they are served through the
// asset store rather than as static files.
app.MapGet(
    StoryAssetEndpoint.Route,
    (IStoryWorkspace workspace, string path, CancellationToken cancellationToken) =>
        StoryAssetEndpoint.HandleAsync(workspace, path, cancellationToken));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
