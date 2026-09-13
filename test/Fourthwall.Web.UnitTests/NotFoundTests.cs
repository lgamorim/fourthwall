using Fourthwall.Web.Components.Pages;

namespace Fourthwall.Web.UnitTests;

public class NotFoundTests : BunitContext
{
    [Fact]
    public void Should_OfferAWayBackToThePicker_When_Rendered()
    {
        // Arrange & Act
        var cut = Render<NotFound>();

        // Assert — a dead address is a moment for direction, not a dead end.
        Assert.Equal("/", cut.Find(".page-link").GetAttribute("href"));
    }
}
