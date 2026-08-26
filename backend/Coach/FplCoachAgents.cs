namespace Backend.Coach;

public static class FplCoachAgents
{
    public const string ParentName = "FplCoachAgent";
    public const string InjurySpecialistName = "InjuryAgent";
    public const string FixtureSpecialistName = "FixtureAgent";
    public const string TransferSpecialistName = "TransferAgent";

    public const string AvailabilityTool = "get_player_availability";
    public const string FixturesTool = "get_upcoming_fixtures";
    public const string TransfersTool = "get_transfer_candidates";
}