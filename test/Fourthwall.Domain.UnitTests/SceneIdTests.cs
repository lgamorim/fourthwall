namespace Fourthwall.Domain.UnitTests;

public class SceneIdTests
{
    [Fact]
    public void Should_ProduceDistinctIds_When_NewIsCalledRepeatedly()
    {
        // Arrange & Act
        var first = SceneId.New();
        var second = SceneId.New();

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Should_ExposeUnderlyingValue_When_Constructed()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var id = new SceneId(value);

        // Assert
        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Should_BeEqual_When_UnderlyingValuesMatch()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var left = new SceneId(value);
        var right = new SceneId(value);

        // Assert
        Assert.Equal(left, right);
    }

    [Fact]
    public void Should_NotBeEqual_When_UnderlyingValuesDiffer()
    {
        // Arrange & Act
        var left = new SceneId(Guid.NewGuid());
        var right = new SceneId(Guid.NewGuid());

        // Assert
        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Should_CarryEmptyValue_When_Default()
    {
        // A default SceneId cannot be prevented on a struct, so usage sites
        // (Choice targets, Story wiring) are responsible for rejecting it.

        // Arrange & Act
        var id = default(SceneId);

        // Assert
        Assert.Equal(Guid.Empty, id.Value);
    }
}
