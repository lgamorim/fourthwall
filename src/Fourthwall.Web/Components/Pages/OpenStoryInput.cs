using System.ComponentModel.DataAnnotations;

namespace Fourthwall.Web.Components.Pages;

// Mutable for the same reason as CreateStoryInput: the EditForm binder assigns it. Whether a
// story exists at the folder is the workspace's answer, not the annotation's.
public sealed class OpenStoryInput
{
    [Required(ErrorMessage = "Enter the folder holding the story.")]
    public string FolderPath { get; set; } = string.Empty;
}
