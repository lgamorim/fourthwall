namespace Fourthwall.Domain.UnitTests;

public class StoryTests
{
    [Fact]
    public void Should_ExposeTitleAndNoScenes_When_Constructed()
    {
        // Arrange & Act
        var story = new Story("The Crossroads");

        // Assert
        Assert.Equal("The Crossroads", story.Title);
        Assert.Empty(story.Scenes);
        Assert.Null(story.StartSceneId);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_TitleIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Story(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_TitleIsBlank(string title)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Story(title));
    }

    [Fact]
    public void Should_UpdateTitle_When_Renamed()
    {
        // Arrange
        var story = new Story("Before");

        // Act
        story.Rename("After");

        // Assert
        Assert.Equal("After", story.Title);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_RenamedToBlank()
    {
        // Arrange
        var story = new Story("Before");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => story.Rename("  "));
    }

    [Fact]
    public void Should_RegisterSceneWithUniqueId_When_SceneAdded()
    {
        // Arrange
        var story = new Story("Story");

        // Act
        var first = story.AddScene(SceneKind.Linear, "one");
        var second = story.AddScene(SceneKind.Linear, "two");

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, story.Scenes.Count);
        Assert.Same(first, story.FindScene(first.Id));
    }

    [Fact]
    public void Should_ReturnNull_When_FindingUnknownScene()
    {
        // Arrange
        var story = new Story("Story");

        // Act
        var found = story.FindScene(SceneId.New());

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void Should_SetStart_When_SceneBelongsToStory()
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Linear, "opening");

        // Act
        story.SetStartScene(scene.Id);

        // Assert
        Assert.Equal(scene.Id, story.StartSceneId);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_StartSetToUnknownScene()
    {
        // Arrange
        var story = new Story("Story");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => story.SetStartScene(SceneId.New()));
    }

    [Fact]
    public void Should_AppendChoiceInOrder_When_ChoicesWired()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        var left = story.AddScene(SceneKind.Ending, "Left", EndingOutcome.Death());
        var right = story.AddScene(SceneKind.Ending, "Right", EndingOutcome.Victory());

        // Act
        story.WireChoice(from.Id, "Go left", left.Id);
        story.WireChoice(from.Id, "Go right", right.Id);

        // Assert
        Assert.Collection(
            from.Choices,
            choice => Assert.Equal("Go left", choice.Label),
            choice => Assert.Equal("Go right", choice.Label));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ChoiceWiredFromUnknownScene()
    {
        // Arrange
        var story = new Story("Story");
        var target = story.AddScene(SceneKind.Linear, "target");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => story.WireChoice(SceneId.New(), "Go", target.Id));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ChoiceWiredToUnknownTarget()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => story.WireChoice(from.Id, "Go", SceneId.New()));
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ChoiceWiredFromEndingScene()
    {
        // Arrange
        var story = new Story("Story");
        var ending = story.AddScene(SceneKind.Ending, "You died.", EndingOutcome.Death());
        var target = story.AddScene(SceneKind.Linear, "target");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => story.WireChoice(ending.Id, "Go", target.Id));
    }

    [Fact]
    public void Should_SetFollowUp_When_SceneIsLinear()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Linear, "corridor");
        var target = story.AddScene(SceneKind.Linear, "hall");

        // Act
        story.SetFollowUp(from.Id, target.Id);

        // Assert
        Assert.Equal(target.Id, from.FollowUpSceneId);
        Assert.Equal([target.Id], from.OutgoingSceneIds);
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_FollowUpSetOnNonLinearScene()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Linear, "hall");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => story.SetFollowUp(from.Id, target.Id));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_FollowUpTargetIsUnknown()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Linear, "corridor");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => story.SetFollowUp(from.Id, SceneId.New()));
    }

    [Fact]
    public void Should_AllowSelfLoop_When_ChoiceTargetsItsOwnScene()
    {
        // Cycles are legal in a gamebook (design doc section 4.2).

        // Arrange
        var story = new Story("Story");
        var riddle = story.AddScene(SceneKind.Choice, "Answer the riddle");

        // Act
        story.WireChoice(riddle.Id, "Try again", riddle.Id);

        // Assert
        Assert.Equal([riddle.Id], riddle.OutgoingSceneIds);
    }

    [Fact]
    public void Should_RemoveInboundChoices_When_TargetSceneRemoved()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        var doomed = story.AddScene(SceneKind.Ending, "Left", EndingOutcome.Death());
        var kept = story.AddScene(SceneKind.Ending, "Right", EndingOutcome.Victory());
        story.WireChoice(from.Id, "Go left", doomed.Id);
        story.WireChoice(from.Id, "Go right", kept.Id);

        // Act
        var removed = story.RemoveScene(doomed.Id);

        // Assert
        Assert.True(removed);
        Assert.Null(story.FindScene(doomed.Id));
        Assert.Single(from.Choices);
        Assert.Equal("Go right", from.Choices[0].Label);
    }

    [Fact]
    public void Should_RemoveInboundFollowUp_When_TargetSceneRemoved()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Linear, "corridor");
        var target = story.AddScene(SceneKind.Linear, "hall");
        story.SetFollowUp(from.Id, target.Id);

        // Act
        story.RemoveScene(target.Id);

        // Assert
        Assert.Null(from.FollowUpSceneId);
        Assert.Empty(from.OutgoingSceneIds);
    }

    [Fact]
    public void Should_ClearStart_When_StartSceneRemoved()
    {
        // Arrange
        var story = new Story("Story");
        var opening = story.AddScene(SceneKind.Linear, "opening");
        story.SetStartScene(opening.Id);

        // Act
        story.RemoveScene(opening.Id);

        // Assert
        Assert.Null(story.StartSceneId);
    }

    [Fact]
    public void Should_ReturnFalse_When_RemovingUnknownScene()
    {
        // Arrange
        var story = new Story("Story");

        // Act
        var removed = story.RemoveScene(SceneId.New());

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void Should_RemoveChoice_When_RemoveChoiceCalled()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        var left = story.AddScene(SceneKind.Linear, "left");
        var right = story.AddScene(SceneKind.Linear, "right");
        story.WireChoice(from.Id, "Go left", left.Id);
        story.WireChoice(from.Id, "Go right", right.Id);

        // Act
        story.RemoveChoice(from.Id, 0);

        // Assert
        Assert.Single(from.Choices);
        Assert.Equal("Go right", from.Choices[0].Label);
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_RemovingChoiceAtInvalidIndex()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => story.RemoveChoice(from.Id, 0));
    }

    [Fact]
    public void Should_ReorderChoices_When_ChoiceMoved()
    {
        // Choice order is the reader-facing order, so moving one reorders the list.

        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Choice, "A fork");
        var left = story.AddScene(SceneKind.Linear, "left");
        var right = story.AddScene(SceneKind.Linear, "right");
        story.WireChoice(from.Id, "Go left", left.Id);
        story.WireChoice(from.Id, "Go right", right.Id);

        // Act
        story.MoveChoice(from.Id, 1, 0);

        // Assert
        Assert.Collection(
            from.Choices,
            choice => Assert.Equal("Go right", choice.Label),
            choice => Assert.Equal("Go left", choice.Label));
    }

    [Fact]
    public void Should_DiscardOutgoingEdges_When_SceneKindChanges()
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Linear, "target");
        story.WireChoice(scene.Id, "Go", target.Id);

        // Act
        scene.ChangeKind(SceneKind.Ending, EndingOutcome.Death());

        // Assert
        Assert.Empty(scene.Choices);
        Assert.Empty(scene.OutgoingSceneIds);
    }

    [Fact]
    public void Should_SurviveRoundTrip_When_BranchingStoryBuiltAndPruned()
    {
        // End-to-end sanity: a start scene branching into two endings, then a
        // targeted scene removed, exercising cascade across the whole aggregate.

        // Arrange
        var story = new Story("The Crossroads");
        var start = story.AddScene(SceneKind.Choice, "A fork in the road.");
        var death = story.AddScene(SceneKind.Ending, "A grue devours you.", EndingOutcome.Death("Eaten by the grue"));
        var victory = story.AddScene(SceneKind.Ending, "You reach the castle.", EndingOutcome.Victory());
        story.SetStartScene(start.Id);
        story.WireChoice(start.Id, "Take the dark path", death.Id);
        story.WireChoice(start.Id, "Take the bright path", victory.Id);

        // Act
        story.RemoveScene(death.Id);

        // Assert
        Assert.Equal(2, story.Scenes.Count);
        Assert.Equal(start.Id, story.StartSceneId);
        Assert.Equal([victory.Id], start.OutgoingSceneIds);
        Assert.Single(start.Choices);
        Assert.Equal("Take the bright path", start.Choices[0].Label);
    }

    [Fact]
    public void Should_PreserveChoices_When_KindIsChangedToTheSameKind()
    {
        // ChangeKind only discards transitions when the kind actually changes. Nothing
        // else pins that, so a refactor hoisting the clear out of the guard would wipe a
        // scene's authored choices silently.

        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Choice, "A fork");
        var left = story.AddScene(SceneKind.Linear, "left");
        var right = story.AddScene(SceneKind.Linear, "right");
        story.WireChoice(scene.Id, "Go left", left.Id);
        story.WireChoice(scene.Id, "Go right", right.Id);

        // Act
        scene.ChangeKind(SceneKind.Choice);

        // Assert
        Assert.Equal(2, scene.Choices.Count);
        Assert.Equal("Go left", scene.Choices[0].Label);
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ChoiceWiredFromLinearScene()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Linear, "a corridor");
        var target = story.AddScene(SceneKind.Linear, "a hall");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => story.WireChoice(from.Id, "Go", target.Id));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void Should_ThrowArgumentOutOfRangeException_When_ChoiceMovedWithInvalidIndex(
        int fromIndex,
        int toIndex)
    {
        // Arrange
        var story = new Story("Story");
        var scene = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Linear, "target");
        story.WireChoice(scene.Id, "Go left", target.Id);
        story.WireChoice(scene.Id, "Go right", target.Id);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => story.MoveChoice(scene.Id, fromIndex, toIndex));
    }

    [Fact]
    public void Should_ClearFollowUp_When_LinearSceneIsUnwired()
    {
        // Arrange
        var story = new Story("Story");
        var from = story.AddScene(SceneKind.Linear, "a corridor");
        var target = story.AddScene(SceneKind.Linear, "a hall");
        story.SetFollowUp(from.Id, target.Id);

        // Act
        story.ClearFollowUp(from.Id);

        // Assert
        Assert.Null(from.FollowUpSceneId);
        Assert.Empty(from.OutgoingSceneIds);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_FollowUpClearedOnUnknownScene()
    {
        // Arrange
        var story = new Story("Story");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => story.ClearFollowUp(SceneId.New()));
    }
}
