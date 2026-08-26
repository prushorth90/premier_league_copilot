using Backend.Models;

namespace Backend.Tests;

public class PlayerPhotoUrlTests
{
    [Fact]
    public void FromCodeBuildsOfficialPremierLeagueCdnUrl()
    {
        var photoUrl = PlayerPhotoUrl.FromCode(244851);

        Assert.Equal(
            "https://resources.premierleague.com/premierleague/photos/players/110x140/p244851.png",
            photoUrl);
        Assert.True(Uri.TryCreate(photoUrl, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri?.Scheme);
        Assert.Equal("resources.premierleague.com", uri?.Host);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromCodeUsesLocalFallbackWhenCodeIsMissing(int code)
    {
        Assert.Equal("/images/player-placeholder.svg", PlayerPhotoUrl.FromCode(code));
    }
}
