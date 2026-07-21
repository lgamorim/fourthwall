using Fourthwall.Domain;

namespace Fourthwall.Application.UnitTests;

public class ValidationViolationTests
{
    [Fact]
    public void Should_ExposeProperties_When_Constructed()
    {
        // Arrange
        var sceneId = SceneId.New();

        // Act
        var violation = new ValidationViolation(
            ValidationRule.AllScenesReachable,
            ValidationSeverity.Error,
            "Unreachable.",
            [sceneId]);

        // Assert
        Assert.Equal(ValidationRule.AllScenesReachable, violation.Rule);
        Assert.Equal(ValidationSeverity.Error, violation.Severity);
        Assert.Equal("Unreachable.", violation.Message);
        Assert.Equal([sceneId], violation.SceneIds);
    }

    [Fact]
    public void Should_AllowEmptySceneIds_When_ViolationIsStoryLevel()
    {
        // A missing start scene is a property of the story, not of any one scene.

        // Arrange & Act
        var violation = new ValidationViolation(
            ValidationRule.SingleStartScene,
            ValidationSeverity.Error,
            "No start scene.",
            []);

        // Assert
        Assert.Empty(violation.SceneIds);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_MessageIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ValidationViolation(
            ValidationRule.SingleStartScene, ValidationSeverity.Error, null!, []));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_MessageIsBlank()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new ValidationViolation(
            ValidationRule.SingleStartScene, ValidationSeverity.Error, "   ", []));
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_SceneIdsIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ValidationViolation(
            ValidationRule.SingleStartScene, ValidationSeverity.Error, "No start scene.", null!));
    }
}
