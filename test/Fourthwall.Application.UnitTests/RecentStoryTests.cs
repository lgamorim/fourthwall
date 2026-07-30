namespace Fourthwall.Application.UnitTests;

public class RecentStoryTests
{
    private static readonly DateTimeOffset LastOpened = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_ThrowArgumentNullException_When_TitleIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RecentStory(null!, @"C:\stories\wreck", LastOpened));
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_FolderPathIsNull()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RecentStory("The Wreck", null!, LastOpened));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_TitleIsBlank(string title)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new RecentStory(title, @"C:\stories\wreck", LastOpened));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_FolderPathIsBlank(string folderPath)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new RecentStory("The Wreck", folderPath, LastOpened));
    }

    [Fact]
    public void Should_ExposeProvidedValues_When_Constructed()
    {
        // Arrange & Act
        var recent = new RecentStory("The Wreck", @"C:\stories\wreck", LastOpened);

        // Assert
        Assert.Equal("The Wreck", recent.Title);
        Assert.Equal(@"C:\stories\wreck", recent.FolderPath);
        Assert.Equal(LastOpened, recent.LastOpenedUtc);
    }
}
