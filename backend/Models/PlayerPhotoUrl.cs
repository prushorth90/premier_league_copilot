namespace Backend.Models;

public static class PlayerPhotoUrl
{
    public const string Fallback = "/images/player-placeholder.svg";

    private const string PremierLeaguePhotoBaseUrl =
        "https://resources.premierleague.com/premierleague/photos/players/110x140";

    public static string FromCode(int code) => code > 0
        ? $"{PremierLeaguePhotoBaseUrl}/p{code}.png"
        : Fallback;
}