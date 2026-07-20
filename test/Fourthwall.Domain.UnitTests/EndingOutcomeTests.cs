namespace Fourthwall.Domain.UnitTests;

public class EndingOutcomeTests
{
    [Fact]
    public void Should_CarryDeathKindWithoutLabel_When_CreatedWithoutLabel()
    {
        // Arrange & Act
        var outcome = EndingOutcome.Death();

        // Assert
        Assert.Equal(OutcomeKind.Death, outcome.Kind);
        Assert.Null(outcome.Label);
    }

    [Fact]
    public void Should_CarryVictoryKindWithoutLabel_When_CreatedWithoutLabel()
    {
        // Arrange & Act
        var outcome = EndingOutcome.Victory();

        // Assert
        Assert.Equal(OutcomeKind.Victory, outcome.Kind);
        Assert.Null(outcome.Label);
    }

    [Fact]
    public void Should_CarryLabel_When_DeathCreatedWithLabel()
    {
        // Arrange & Act
        var outcome = EndingOutcome.Death("Eaten by the grue");

        // Assert
        Assert.Equal(OutcomeKind.Death, outcome.Kind);
        Assert.Equal("Eaten by the grue", outcome.Label);
    }

    [Fact]
    public void Should_CarryLabel_When_OtherCreatedWithLabel()
    {
        // Arrange & Act
        var outcome = EndingOutcome.Other("Became the king's advisor");

        // Assert
        Assert.Equal(OutcomeKind.Other, outcome.Kind);
        Assert.Equal("Became the king's advisor", outcome.Label);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_OtherCreatedWithNullLabel()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => EndingOutcome.Other(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Should_ThrowArgumentException_When_OtherCreatedWithBlankLabel(string label)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => EndingOutcome.Other(label));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_DeathCreatedWithBlankLabel(string label)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => EndingOutcome.Death(label));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_VictoryCreatedWithBlankLabel(string label)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => EndingOutcome.Victory(label));
    }

    [Fact]
    public void Should_BeEqual_When_KindAndLabelMatch()
    {
        // Arrange & Act
        var left = EndingOutcome.Death("Drowned");
        var right = EndingOutcome.Death("Drowned");

        // Assert
        Assert.Equal(left, right);
    }

    [Fact]
    public void Should_NotBeEqual_When_KindsDiffer()
    {
        // Arrange & Act
        var death = EndingOutcome.Death();
        var victory = EndingOutcome.Victory();

        // Assert
        Assert.NotEqual(death, victory);
    }

    [Fact]
    public void Should_NotBeEqual_When_LabelsDiffer()
    {
        // Arrange & Act
        var left = EndingOutcome.Death("Drowned");
        var right = EndingOutcome.Death("Burned");

        // Assert
        Assert.NotEqual(left, right);
    }
}
