namespace Fourthwall.Domain.UnitTests;

public class SceneTests
{
    [Fact]
    public void Should_ExposeProperties_When_Constructed()
    {
        // Arrange
        var id = SceneId.New();

        // Act
        var scene = new Scene(id, SceneKind.Choice, "A fork in the road.");

        // Assert
        Assert.Equal(id, scene.Id);
        Assert.Equal(SceneKind.Choice, scene.Kind);
        Assert.Equal("A fork in the road.", scene.Text);
        Assert.Null(scene.ImagePath);
        Assert.Null(scene.Outcome);
        Assert.Empty(scene.Choices);
        Assert.Null(scene.FollowUpSceneId);
        Assert.Empty(scene.OutgoingSceneIds);
    }

    [Fact]
    public void Should_AllowEmptyText_When_Constructed()
    {
        // Narrative text may be empty while a scene is still being authored.

        // Arrange & Act
        var scene = new Scene(SceneId.New(), SceneKind.Linear, string.Empty);

        // Assert
        Assert.Equal(string.Empty, scene.Text);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_TextIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Scene(SceneId.New(), SceneKind.Linear, null!));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_IdIsDefault()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Scene(default, SceneKind.Linear, "text"));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_KindIsNotDefined()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Scene(SceneId.New(), (SceneKind)99, "text"));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_EndingConstructedWithoutOutcome()
    {
        // An ending always carries an outcome (design doc section 4.1).

        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new Scene(SceneId.New(), SceneKind.Ending, "You died."));
    }

    [Theory]
    [InlineData(SceneKind.Choice)]
    [InlineData(SceneKind.Linear)]
    public void Should_ThrowArgumentException_When_NonEndingConstructedWithOutcome(SceneKind kind)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(
            () => new Scene(SceneId.New(), kind, "text", EndingOutcome.Victory()));
    }

    [Fact]
    public void Should_CarryOutcomeAndNoOutgoingEdges_When_EndingConstructed()
    {
        // Arrange & Act
        var scene = new Scene(SceneId.New(), SceneKind.Ending, "You died.", EndingOutcome.Death());

        // Assert
        Assert.Equal(EndingOutcome.Death(), scene.Outcome);
        Assert.Empty(scene.Choices);
        Assert.Null(scene.FollowUpSceneId);
        Assert.Empty(scene.OutgoingSceneIds);
    }

    [Fact]
    public void Should_UpdateText_When_SetTextCalled()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "before");

        // Act
        scene.SetText("after");

        // Assert
        Assert.Equal("after", scene.Text);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_SetTextGivenNull()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "before");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => scene.SetText(null!));
    }

    [Fact]
    public void Should_SetImagePath_When_ImageAttached()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act
        scene.AttachImage("assets/crossroads.png");

        // Assert
        Assert.Equal("assets/crossroads.png", scene.ImagePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_ImageAttachedWithBlankPath(string path)
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scene.AttachImage(path));
    }

    [Fact]
    public void Should_ClearImagePath_When_ImageCleared()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");
        scene.AttachImage("assets/crossroads.png");

        // Act
        scene.ClearImage();

        // Assert
        Assert.Null(scene.ImagePath);
    }

    [Fact]
    public void Should_SetOutcome_When_ChangedToEnding()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act
        scene.ChangeKind(SceneKind.Ending, EndingOutcome.Victory("Crowned at dawn"));

        // Assert
        Assert.Equal(SceneKind.Ending, scene.Kind);
        Assert.Equal(EndingOutcome.Victory("Crowned at dawn"), scene.Outcome);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ChangedToEndingWithoutOutcome()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scene.ChangeKind(SceneKind.Ending));
    }

    [Fact]
    public void Should_ClearOutcome_When_ChangedFromEndingToChoice()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Ending, "You died.", EndingOutcome.Death());

        // Act
        scene.ChangeKind(SceneKind.Choice);

        // Assert
        Assert.Equal(SceneKind.Choice, scene.Kind);
        Assert.Null(scene.Outcome);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ChangedToNonEndingWithOutcome()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => scene.ChangeKind(SceneKind.Choice, EndingOutcome.Death()));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ChangedToUndefinedKind()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scene.ChangeKind((SceneKind)99));
    }

    [Fact]
    public void Should_NotExposeItsMutableList_When_ChoicesAreRead()
    {
        // The aggregate's guarantee rests on transitions being mutable only through the
        // owning story. Handing out the backing list would let any caller cast back to
        // List<Choice> and bypass that entirely.

        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Choice, "A fork");

        // Act & Assert
        Assert.IsNotType<List<Choice>>(scene.Choices);
    }

    [Fact]
    public void Should_ReplaceOutcome_When_EndingIsGivenANewOutcome()
    {
        // Re-applying the Ending kind is the only way to edit an ending's outcome.

        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Ending, "The end.", EndingOutcome.Death());

        // Act
        scene.ChangeKind(SceneKind.Ending, EndingOutcome.Victory("Crowned at dawn"));

        // Assert
        Assert.Equal(EndingOutcome.Victory("Crowned at dawn"), scene.Outcome);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_EndingKindIsReappliedWithoutOutcome()
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Ending, "The end.", EndingOutcome.Death());

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scene.ChangeKind(SceneKind.Ending));
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("\\windows\\system32")]
    [InlineData("C:/images/x.png")]
    [InlineData("C:\\images\\x.png")]
    [InlineData("../secrets.png")]
    [InlineData("..\\secrets.png")]
    [InlineData("assets/../../secrets.png")]
    [InlineData("assets\\..\\..\\secrets.png")]
    public void Should_ThrowArgumentException_When_ImagePathEscapesTheStoryFolder(string path)
    {
        // The path is resolved against the story folder once persistence arrives, so a
        // rooted or traversing path would escape the package.

        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scene.AttachImage(path));
    }

    [Theory]
    [InlineData("crossroads.png")]
    [InlineData("assets/crossroads.png")]
    [InlineData("assets\\crossroads.png")]
    [InlineData("assets/sub/deep.png")]
    [InlineData("assets/..hidden/x.png")]
    public void Should_AttachImage_When_PathStaysInsideTheStoryFolder(string path)
    {
        // Arrange
        var scene = new Scene(SceneId.New(), SceneKind.Linear, "text");

        // Act
        scene.AttachImage(path);

        // Assert
        Assert.Equal(path, scene.ImagePath);
    }
}
