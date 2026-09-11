using Fourthwall.Domain;

namespace Fourthwall.Web.Components.Canvas;

/// <summary>
/// Identifies one outgoing transition of a scene: a choice at <paramref name="ChoiceIndex"/>, or
/// the scene's follow-up when <paramref name="ChoiceIndex"/> is <see langword="null"/>.
/// </summary>
/// <param name="Source">The scene the transition leads from.</param>
/// <param name="ChoiceIndex">
/// The position of the choice this key identifies, or <see langword="null"/> for a follow-up.
/// </param>
public readonly record struct CanvasEdgeKey(SceneId Source, int? ChoiceIndex);
