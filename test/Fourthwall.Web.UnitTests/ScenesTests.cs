using Fourthwall.Domain;
using Fourthwall.Web.Components.Editor;

namespace Fourthwall.Web.UnitTests;

public class ScenesTests
{
    [Fact]
    public void Should_KeepTheWholeText_When_ItFitsTheMaximumLength()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "  A storm  ");

        // Act
        var label = Scenes.Label(scene, maxLength: 7);

        // Assert — surrounding whitespace is not part of the label.
        Assert.Equal("A storm", label);
    }

    [Fact]
    public void Should_CutAndAddAnEllipsis_When_TheTextIsLongerThanTheMaximumLength()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "A storm gathers");

        // Act
        var label = Scenes.Label(scene, maxLength: 7);

        // Assert
        Assert.Equal("A storm…", label);
    }

    [Fact]
    public void Should_StandInForTheText_When_TheSceneHasNone()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "   ");

        // Act
        var label = Scenes.Label(scene, maxLength: 7);

        // Assert
        Assert.Equal("(no text)", label);
    }

    [Fact]
    public void Should_NotSplitASurrogatePair_When_TheCutLandsInsideOne()
    {
        // Arrange — "ab" then an emoji (two UTF-16 units): a cut at 3 would keep half of it.
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "ab\U0001F30Acd");

        // Act
        var label = Scenes.Label(scene, maxLength: 3);

        // Assert
        Assert.Equal("ab…", label);
    }

    [Fact]
    public void Should_UseTheListLength_When_NoMaximumIsGiven()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, new string('a', 61));

        // Act
        var label = Scenes.Label(scene);

        // Assert — the navigator's and dropdowns' 60-character label is unchanged.
        Assert.Equal(new string('a', 60) + "…", label);
    }

    [Fact]
    public void Should_CutAChoiceLabel_When_ItIsLongerThanTheMaximumLength()
    {
        // Arrange & Act
        var label = Scenes.Truncate("Swim for the lifeboat", maxLength: 8);

        // Assert
        Assert.Equal("Swim for…", label);
    }

    [Fact]
    public void Should_KeepAChoiceLabel_When_ItFits()
    {
        // Arrange & Act
        var label = Scenes.Truncate("Swim", maxLength: 8);

        // Assert
        Assert.Equal("Swim", label);
    }

    [Fact]
    public void Should_Throw_When_TheMaximumLengthIsNotPositive()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "A storm");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Scenes.Label(scene, maxLength: 0));
    }
}
