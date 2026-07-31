using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.UnitTests;

public class SceneInspectorTests : BunitContext
{
    [Fact]
    public void Should_UpdateTheText_When_ItIsCommitted()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        var changed = 0;
        var cut = RenderFor(scene, () => changed++);

        // Act
        cut.Find("#inspector-text").Change("The mast splits.");

        // Assert
        Assert.Equal("The mast splits.", scene.Text);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_AcceptEmptyText_When_ItIsCleared()
    {
        // Arrange — the Domain allows empty text while authoring, so the form must not invent a rule.
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderFor(scene);

        // Act
        cut.Find("#inspector-text").Change(string.Empty);

        // Assert
        Assert.Equal(string.Empty, scene.Text);
    }

    [Fact]
    public void Should_ChangeTheKind_When_NothingWouldBeDiscarded()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        var changed = 0;
        var cut = RenderFor(scene, () => changed++);

        // Act
        cut.Find("#inspector-kind").Change(nameof(SceneKind.Choice));

        // Assert — no prompt: an empty Linear scene has no transitions to lose.
        Assert.Equal(SceneKind.Choice, scene.Kind);
        Assert.Empty(cut.FindAll(".inspector-kind-confirm"));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_AskFirst_When_TheKindChangeWouldDiscardTransitions()
    {
        // Arrange
        var scene = LinearWithFollowUp();
        var changed = 0;
        var cut = RenderFor(scene, () => changed++);

        // Act
        cut.Find("#inspector-kind").Change(nameof(SceneKind.Choice));

        // Assert
        Assert.Equal(SceneKind.Linear, scene.Kind);
        Assert.NotNull(cut.Find(".inspector-kind-confirm"));
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Should_ChangeTheKind_When_TheDiscardIsConfirmed()
    {
        // Arrange
        var scene = LinearWithFollowUp();
        var cut = RenderFor(scene);
        cut.Find("#inspector-kind").Change(nameof(SceneKind.Choice));

        // Act
        cut.Find(".inspector-kind-confirm").Click();

        // Assert
        Assert.Equal(SceneKind.Choice, scene.Kind);
        Assert.Null(scene.FollowUpSceneId);
    }

    [Fact]
    public void Should_KeepTheKind_When_TheDiscardIsCancelled()
    {
        // Arrange
        var scene = LinearWithFollowUp();
        var cut = RenderFor(scene);
        cut.Find("#inspector-kind").Change(nameof(SceneKind.Choice));

        // Act
        cut.Find(".inspector-kind-cancel").Click();

        // Assert
        Assert.Equal(SceneKind.Linear, scene.Kind);
        Assert.NotNull(scene.FollowUpSceneId);
        Assert.Empty(cut.FindAll(".inspector-kind-confirm"));
    }

    [Fact]
    public void Should_ChangeTheOutcome_When_AnotherIsChosen()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Ending, "You drown.", EndingOutcome.Death());
        var changed = 0;
        var cut = RenderFor(scene, () => changed++);

        // Act
        cut.Find("#inspector-outcome").Change(nameof(OutcomeKind.Victory));

        // Assert
        Assert.Equal(OutcomeKind.Victory, scene.Outcome!.Kind);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_RefuseTheOutcome_When_OtherHasNoLabel()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Ending, "You drown.", EndingOutcome.Death());
        var cut = RenderFor(scene);

        // Act
        cut.Find("#inspector-outcome").Change(nameof(OutcomeKind.Other));

        // Assert
        Assert.Equal(OutcomeKind.Death, scene.Outcome!.Kind);
        Assert.NotNull(cut.Find(".inspector-error"));
    }

    [Fact]
    public void Should_KeepTheOutcomeLabel_When_OneIsGiven()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Ending, "You drown.", EndingOutcome.Death());
        var cut = RenderFor(scene);
        cut.Find("#inspector-outcome").Change(nameof(OutcomeKind.Other));

        // Act
        cut.Find("#inspector-outcome-label").Change("Lost at sea");

        // Assert
        Assert.Equal(OutcomeKind.Other, scene.Outcome!.Kind);
        Assert.Equal("Lost at sea", scene.Outcome.Label);
    }

    [Fact]
    public void Should_GiveTheEndingAnOutcome_When_TheKindBecomesEnding()
    {
        // Arrange — an Ending must carry an outcome, so changing kind has to supply one.
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = RenderFor(scene);

        // Act
        cut.Find("#inspector-kind").Change(nameof(SceneKind.Ending));

        // Assert
        Assert.Equal(SceneKind.Ending, scene.Kind);
        Assert.NotNull(scene.Outcome);
    }

    [Fact]
    public void Should_ShowNoOutcomeControls_When_TheSceneIsNotAnEnding()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderFor(scene);

        // Assert
        Assert.Empty(cut.FindAll("#inspector-outcome"));
    }

    [Fact]
    public void Should_ShowTheNewScene_When_TheSelectionChanges()
    {
        // Arrange
        var story = new Story("The Wreck");
        var first = story.AddScene(SceneKind.Linear, "A storm gathers");
        var second = story.AddScene(SceneKind.Linear, "Below deck");
        var cut = RenderFor(first);

        // Act
        cut.Render(parameters => parameters.Add(p => p.Scene, second));

        // Assert
        Assert.Equal("Below deck", cut.Find("#inspector-text").GetAttribute("value") ??
            cut.Find("#inspector-text").TextContent);
    }

    private static Scene LinearWithFollowUp()
    {
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        var ending = story.AddScene(SceneKind.Ending, "You drown.", EndingOutcome.Death());
        story.SetFollowUp(scene.Id, ending.Id);
        return scene;
    }

    private IRenderedComponent<SceneInspector> RenderFor(Scene scene, Action? onChanged = null) =>
        Render<SceneInspector>(parameters => parameters
            .Add(p => p.Scene, scene)
            .Add(p => p.OnChanged, () => onChanged?.Invoke()));
}
