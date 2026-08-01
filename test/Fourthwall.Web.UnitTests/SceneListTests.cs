using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.UnitTests;

public class SceneListTests : BunitContext
{
    [Fact]
    public void Should_ListScenesAlphabetically_When_NoStartSceneIsSet()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "Below deck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        story.AddScene(SceneKind.Linear, "cabin door");

        // Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Assert — case-insensitive, so "cabin door" sorts between the capitals.
        Assert.Equal(
            ["A storm gathers", "Below deck", "cabin door"],
            cut.FindAll(".scene-snippet").Select(row => row.TextContent.Trim()));
    }

    [Fact]
    public void Should_PinTheStartScene_When_OneIsSet()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var last = story.AddScene(SceneKind.Ending, "Zero hour", EndingOutcome.Death());
        story.SetStartScene(last.Id);

        // Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Assert
        Assert.Equal(
            ["Zero hour", "A storm gathers"],
            cut.FindAll(".scene-snippet").Select(row => row.TextContent.Trim()));
        Assert.Single(cut.FindAll(".scene-start"));
    }

    [Fact]
    public void Should_OrderByIdentifier_When_TextIsIdentical()
    {
        // Arrange — identical text must still produce a stable order.
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "same");
        story.AddScene(SceneKind.Linear, "same");
        var expected = story.Scenes.OrderBy(scene => scene.Id.Value).Select(scene => scene.Id).ToList();

        // Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Assert
        var rendered = cut.FindAll(".scene-row").Select(row => new SceneId(Guid.Parse(row.GetAttribute("data-scene-id")!)));
        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void Should_ShowAPlaceholder_When_ASceneHasNoText()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, string.Empty);

        // Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Assert — empty text is legal while authoring, so the row still needs a label.
        Assert.NotEmpty(cut.Find(".scene-snippet").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowAnEmptyState_When_TheStoryHasNoScenes()
    {
        // Arrange & Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, new Story("The Wreck")));

        // Assert
        Assert.NotNull(cut.Find(".scene-empty"));
        Assert.Empty(cut.FindAll(".scene-row"));
    }

    [Fact]
    public void Should_WarnThatNoSceneStartsTheStory_When_ScenesExistWithoutAStart()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");

        // Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Assert — nothing validates this until M16, so the list has to surface it.
        Assert.NotNull(cut.Find(".scene-no-start"));
    }

    [Fact]
    public void Should_ReportTheSelection_When_ASceneIsChosen()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        SceneId? selected = null;
        var cut = Render<SceneList>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.SelectedSceneIdChanged, id => selected = id));

        // Act
        cut.Find(".scene-select").Click();

        // Assert
        Assert.Equal(scene.Id, selected);
    }

    [Fact]
    public void Should_AddTheScene_When_CreateIsSubmitted()
    {
        // Arrange
        var story = new Story("The Wreck");
        var changed = 0;
        var cut = Render<SceneList>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.OnChanged, () => changed++));
        cut.Find("#scene-create-text").Change("A storm gathers");

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert
        var scene = Assert.Single(story.Scenes);
        Assert.Equal("A storm gathers", scene.Text);
        Assert.Equal(SceneKind.Linear, scene.Kind);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_AddAnEndingWithItsOutcome_When_EndingIsChosen()
    {
        // Arrange
        var story = new Story("The Wreck");
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));
        cut.Find("#scene-create-kind").Change(nameof(SceneKind.Ending));
        cut.Find("#scene-create-text").Change("You drown.");
        cut.Find("#scene-create-outcome").Change(nameof(OutcomeKind.Victory));

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert
        var scene = Assert.Single(story.Scenes);
        Assert.Equal(SceneKind.Ending, scene.Kind);
        Assert.Equal(OutcomeKind.Victory, scene.Outcome!.Kind);
    }

    [Fact]
    public void Should_RefuseToAdd_When_OtherOutcomeHasNoLabel()
    {
        // Arrange — EndingOutcome.Other requires a label, so the form must ask for one.
        var story = new Story("The Wreck");
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));
        cut.Find("#scene-create-kind").Change(nameof(SceneKind.Ending));
        cut.Find("#scene-create-outcome").Change(nameof(OutcomeKind.Other));

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert
        Assert.Empty(story.Scenes);
        Assert.NotNull(cut.Find(".scene-create-error"));
    }

    [Fact]
    public void Should_SelectTheNewScene_When_OneIsAdded()
    {
        // Arrange
        var story = new Story("The Wreck");
        SceneId? selected = null;
        var cut = Render<SceneList>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.SelectedSceneIdChanged, id => selected = id));
        cut.Find("#scene-create-text").Change("A storm gathers");

        // Act
        cut.Find("#scene-create-submit").Click();

        // Assert
        Assert.Equal(story.Scenes.Single().Id, selected);
    }

    [Fact]
    public void Should_SetTheStartScene_When_MakeStartIsChosen()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        var changed = 0;
        var cut = Render<SceneList>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.OnChanged, () => changed++));

        // Act
        cut.Find(".scene-make-start").Click();

        // Assert
        Assert.Equal(scene.Id, story.StartSceneId);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_NotDeleteTheScene_When_DeleteIsChosenOnce()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Act
        cut.Find(".scene-delete").Click();

        // Assert — deletion also strips inbound transitions, so it asks first.
        Assert.Single(story.Scenes);
        Assert.NotNull(cut.Find(".scene-delete-confirm"));
    }

    [Fact]
    public void Should_DeleteTheScene_When_DeleteIsConfirmed()
    {
        // Arrange
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var changed = 0;
        var cut = Render<SceneList>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.OnChanged, () => changed++));
        cut.Find(".scene-delete").Click();

        // Act
        cut.Find(".scene-delete-confirm").Click();

        // Assert
        Assert.Empty(story.Scenes);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Should_NameTheInboundTransitions_When_TheSceneIsATarget()
    {
        // Arrange — deleting a scene silently strips every transition pointing at it, so the
        // confirmation should say what else goes.
        var story = new Story("The Wreck");
        var fork = story.AddScene(SceneKind.Choice, "A fork");
        var target = story.AddScene(SceneKind.Linear, "Below deck");
        story.WireChoice(fork.Id, "Go left", target.Id);
        story.WireChoice(fork.Id, "Go right", target.Id);
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Act
        cut.FindAll(".scene-row").Single(row => row.TextContent.Contains("Below deck", StringComparison.Ordinal))
            .QuerySelector(".scene-delete")!.Click();

        // Assert
        Assert.Contains("2", cut.Find(".scene-delete-confirm").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_NotMentionTransitions_When_NothingPointsAtTheScene()
    {
        // Arrange — the same "only warn when there is something to lose" rule kind changes follow.
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Act
        cut.Find(".scene-delete").Click();

        // Assert
        Assert.Equal("Confirm delete", cut.Find(".scene-delete-confirm").TextContent.Trim());
    }

    [Fact]
    public void Should_KeepTheScene_When_DeleteIsCancelled()
    {
        // Arrange — the kind-change prompt offers a way out, and so must this one.
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, "A storm gathers");
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));
        cut.Find(".scene-delete").Click();

        // Act
        cut.Find(".scene-delete-cancel").Click();

        // Assert
        Assert.Single(story.Scenes);
        Assert.Empty(cut.FindAll(".scene-delete-confirm"));
    }

    [Fact]
    public void Should_NotSplitASurrogatePair_When_TheSnippetIsTruncated()
    {
        // Arrange — truncating mid-pair leaves half an emoji, which renders as a broken glyph.
        var story = new Story("The Wreck");
        story.AddScene(SceneKind.Linear, new string('a', 59) + "\U0001F600 and more text after it");

        // Act
        var cut = Render<SceneList>(parameters => parameters.Add(p => p.Story, story));

        // Assert
        Assert.Equal(new string('a', 59) + "…", cut.Find(".scene-snippet").TextContent.Trim());
    }

    [Fact]
    public void Should_ClearTheSelection_When_TheSelectedSceneIsDeleted()
    {
        // Arrange
        var story = new Story("The Wreck");
        var scene = story.AddScene(SceneKind.Linear, "A storm gathers");
        SceneId? selected = scene.Id;
        var cut = Render<SceneList>(parameters => parameters
            .Add(p => p.Story, story)
            .Add(p => p.SelectedSceneId, scene.Id)
            .Add(p => p.SelectedSceneIdChanged, id => selected = id));
        cut.Find(".scene-delete").Click();

        // Act
        cut.Find(".scene-delete-confirm").Click();

        // Assert
        Assert.Null(selected);
    }
}
