using System.ComponentModel.DataAnnotations;

namespace Fourthwall.Web.Components.Pages;

// A form input model, so it is mutable by design — the EditForm binder assigns these properties.
// The annotations cover shape only: that a folder and a title were typed at all. Whether that
// folder can actually hold a new story is a domain question the workspace answers, and its
// failures surface through the page's own error line rather than through validation.
public sealed class CreateStoryInput
{
    [Required(ErrorMessage = "Enter the folder to create the story in.")]
    public string FolderPath { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a title for the story.")]
    public string Title { get; set; } = string.Empty;
}
