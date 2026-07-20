namespace Fourthwall.Domain.UnitTests;

public class ChoiceTests
{
    [Fact]
    public void Should_ExposeLabelAndTarget_When_Constructed()
    {
        // Arrange
        var target = SceneId.New();

        // Act
        var choice = new Choice("Open the door", target);

        // Assert
        Assert.Equal("Open the door", choice.Label);
        Assert.Equal(target, choice.TargetSceneId);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_LabelIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Choice(null!, SceneId.New()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Should_ThrowArgumentException_When_LabelIsBlank(string label)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Choice(label, SceneId.New()));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_TargetIsDefault()
    {
        // A choice must always point at a scene (design doc section 5.1).

        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Choice("Draw your sword", default));
    }

    [Fact]
    public void Should_BeEqual_When_LabelAndTargetMatch()
    {
        // Arrange
        var target = SceneId.New();

        // Act
        var left = new Choice("Flee", target);
        var right = new Choice("Flee", target);

        // Assert
        Assert.Equal(left, right);
    }

    [Fact]
    public void Should_NotBeEqual_When_LabelsDiffer()
    {
        // Arrange
        var target = SceneId.New();

        // Act
        var left = new Choice("Flee", target);
        var right = new Choice("Fight", target);

        // Assert
        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Should_NotBeEqual_When_TargetsDiffer()
    {
        // Arrange & Act
        var left = new Choice("Flee", SceneId.New());
        var right = new Choice("Flee", SceneId.New());

        // Assert
        Assert.NotEqual(left, right);
    }
}
