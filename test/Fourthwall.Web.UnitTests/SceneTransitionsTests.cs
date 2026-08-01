using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.UnitTests;

public class SceneTransitionsTests : BunitContext
{
    [Fact]
    public void Should_ListChoicesInOrder_When_TheSceneHasThem()
    {
        // Arrange
        var story = Fork(out var from, out _, out _);

        // Act
        var cut = RenderFor(story, from);

        // Assert — the authored order is the order the reader sees.
        Assert.Equal(
            ["Go left", "Go right"],
            cut.FindAll(".choice-label").Select(input => input.GetAttribute("value")));
    }

    [Fact]
    public void Should_AddTheChoice_When_AddIsSubmitted()
    {
        // Arrange
        var story = new Story("The Wreck");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Linear, "left");
        var changed = 0;
        var cut = RenderFor(story, from, () => changed++);
        cut.Find("#choice-add-label").Change("Go left");
        cut.Find("#choice-add-target").Change(target.Id.Value.ToString());

        // Act
        cut.Find("#choice-add-submit").Click();

        // Assert
        var choice = Assert.Single(from.Choices);
        Assert.Equal("Go left", choice.Label);
        Assert.Equal(target.Id, choice.TargetSceneId);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_RefuseTheChoice_When_TheLabelIsBlank()
    {
        // Arrange — a choice always carries reader-facing text.
        var story = new Story("The Wreck");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        story.AddScene(SceneKind.Linear, "left");
        var cut = RenderFor(story, from);

        // Act
        cut.Find("#choice-add-submit").Click();

        // Assert
        Assert.Empty(from.Choices);
        Assert.NotNull(cut.Find(".choice-add-error"));
    }

    [Fact]
    public void Should_RelabelTheChoice_When_ItsLabelIsCommitted()
    {
        // Arrange
        var story = Fork(out var from, out var left, out _);
        var changed = 0;
        var cut = RenderFor(story, from, () => changed++);

        // Act
        cut.FindAll(".choice-label")[0].Change("Take the left path");

        // Assert — the choice keeps its position and its target.
        Assert.Equal("Take the left path", from.Choices[0].Label);
        Assert.Equal(left.Id, from.Choices[0].TargetSceneId);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_KeepTheLabel_When_ARelabelIsBlank()
    {
        // Arrange
        var story = Fork(out var from, out _, out _);
        var cut = RenderFor(story, from);

        // Act
        cut.FindAll(".choice-label")[0].Change("   ");

        // Assert
        Assert.Equal("Go left", from.Choices[0].Label);
        Assert.NotNull(cut.Find(".choice-error"));
    }

    [Fact]
    public void Should_RetargetTheChoice_When_AnotherTargetIsChosen()
    {
        // Arrange
        var story = Fork(out var from, out _, out var right);
        var changed = 0;
        var cut = RenderFor(story, from, () => changed++);

        // Act
        cut.FindAll(".choice-target")[0].Change(right.Id.Value.ToString());

        // Assert — the label and the position survive.
        Assert.Equal("Go left", from.Choices[0].Label);
        Assert.Equal(right.Id, from.Choices[0].TargetSceneId);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_OfferTheSceneItself_When_ChoosingATarget()
    {
        // Gamebooks loop: a choice may lead back to the scene it came from (design section 4.2).

        // Arrange
        var story = Fork(out var from, out _, out _);

        // Act
        var cut = RenderFor(story, from);

        // Assert
        var options = cut.FindAll("#choice-add-target option").Select(option => option.GetAttribute("value"));
        Assert.Contains(from.Id.Value.ToString(), options);
    }

    [Fact]
    public void Should_ReorderChoices_When_OneIsMovedUp()
    {
        // Arrange
        var story = Fork(out var from, out _, out _);
        var changed = 0;
        var cut = RenderFor(story, from, () => changed++);

        // Act
        cut.FindAll(".choice-up")[0].Click();

        // Assert
        Assert.Equal(["Go right", "Go left"], from.Choices.Select(choice => choice.Label));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_ReorderChoices_When_OneIsMovedDown()
    {
        // Arrange
        var story = Fork(out var from, out _, out _);
        var cut = RenderFor(story, from);

        // Act
        cut.FindAll(".choice-down")[0].Click();

        // Assert
        Assert.Equal(["Go right", "Go left"], from.Choices.Select(choice => choice.Label));
    }

    [Fact]
    public void Should_OfferNoMoveUp_When_TheChoiceIsFirst()
    {
        // Arrange
        var story = Fork(out var from, out _, out _);

        // Act
        var cut = RenderFor(story, from);

        // Assert — two choices, but only the second can move up.
        Assert.Single(cut.FindAll(".choice-up"));
        Assert.Single(cut.FindAll(".choice-down"));
    }

    [Fact]
    public void Should_RemoveTheChoice_When_RemoveIsChosen()
    {
        // Arrange — no confirmation: a choice is a label and a target, unlike a scene.
        var story = Fork(out var from, out _, out _);
        var changed = 0;
        var cut = RenderFor(story, from, () => changed++);

        // Act
        cut.FindAll(".choice-remove")[0].Click();

        // Assert
        Assert.Equal("Go right", Assert.Single(from.Choices).Label);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_HintForMoreChoices_When_TheSceneHasFewerThanTwo()
    {
        // Arrange — legal while authoring; validation reports it in M16.
        var story = new Story("The Wreck");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        story.AddScene(SceneKind.Linear, "left");

        // Act
        var cut = RenderFor(story, from);

        // Assert
        Assert.NotNull(cut.Find(".choice-hint"));
    }

    [Fact]
    public void Should_NotHint_When_TheSceneHasTwoChoices()
    {
        // Arrange
        var story = Fork(out var from, out _, out _);

        // Act
        var cut = RenderFor(story, from);

        // Assert
        Assert.Empty(cut.FindAll(".choice-hint"));
    }

    [Fact]
    public void Should_SetTheFollowUp_When_ASceneIsChosen()
    {
        // Arrange
        var story = new Story("The Wreck");
        var from = story.AddScene(SceneKind.Linear, "A storm gathers");
        var next = story.AddScene(SceneKind.Linear, "Below deck");
        var changed = 0;
        var cut = RenderFor(story, from, () => changed++);

        // Act
        cut.Find("#follow-up").Change(next.Id.Value.ToString());

        // Assert
        Assert.Equal(next.Id, from.FollowUpSceneId);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_ClearTheFollowUp_When_NoneIsChosen()
    {
        // Arrange
        var story = new Story("The Wreck");
        var from = story.AddScene(SceneKind.Linear, "A storm gathers");
        var next = story.AddScene(SceneKind.Linear, "Below deck");
        story.SetFollowUp(from.Id, next.Id);
        var cut = RenderFor(story, from);

        // Act
        cut.Find("#follow-up").Change(string.Empty);

        // Assert
        Assert.Null(from.FollowUpSceneId);
    }

    [Fact]
    public void Should_HintForAFollowUp_When_ALinearSceneHasNone()
    {
        // Arrange
        var story = new Story("The Wreck");
        var from = story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = RenderFor(story, from);

        // Assert
        Assert.NotNull(cut.Find(".follow-up-hint"));
    }

    [Fact]
    public void Should_ShowNoTransitions_When_TheSceneIsAnEnding()
    {
        // Arrange — an ending terminates the story by construction.
        var story = new Story("The Wreck");
        var ending = story.AddScene(SceneKind.Ending, "You drown.", EndingOutcome.Death());

        // Act
        var cut = RenderFor(story, ending);

        // Assert
        Assert.Empty(cut.FindAll("#follow-up"));
        Assert.Empty(cut.FindAll("#choice-add-submit"));
    }

    private static Story Fork(out Scene from, out Scene left, out Scene right)
    {
        var story = new Story("The Wreck");
        from = story.AddScene(SceneKind.Choice, "A fork");
        left = story.AddScene(SceneKind.Linear, "left");
        right = story.AddScene(SceneKind.Linear, "right");
        story.WireChoice(from.Id, "Go left", left.Id);
        story.WireChoice(from.Id, "Go right", right.Id);
        return story;
    }

    private IRenderedComponent<SceneTransitions> RenderFor(Story story, Scene scene, Action? onChanged = null) =>
        Render<SceneTransitions>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.Scene, scene)
            .Add(p => p.OnChanged, () => onChanged?.Invoke()));
}
