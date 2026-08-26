using Backend.Coach;
using Backend.Coach.Models;

namespace Backend.Tests;

public class PlayerRecommendationServiceTests
{
    [Fact]
    public void EvaluateTransfersUnavailablePlayerWhenLegalCandidateClearsThreshold()
    {
        var result = PlayerRecommendationPolicy.Evaluate(
            Availability("i", "Injured", 95m),
            Fixtures("Mixed"),
            Transfers(Candidate(4m, 82m)));

        Assert.Equal(PlayerRecommendationAction.Transfer, result.Action);
        Assert.Equal(4m, result.ProjectedImpact);
        Assert.Equal(20, result.RecommendedReplacement?.Player.PlayerId);
        Assert.Contains("budget, position, ownership, and club constraints", result.Source);
        Assert.InRange(result.Confidence, 0m, 100m);
    }

    [Theory]
    [InlineData("i", "Mixed", "Injured")]
    [InlineData("d", "Favorable", "Doubtful")]
    [InlineData("a", "Difficult", "Available")]
    public void EvaluateBenchesRiskyPlayerWithoutSufficientTransferGain(
        string status,
        string scheduleRating,
        string statusDescription)
    {
        var result = PlayerRecommendationPolicy.Evaluate(
            Availability(status, statusDescription, 90m),
            Fixtures(scheduleRating),
            Transfers(Candidate(2.99m, 80m)));

        Assert.Equal(PlayerRecommendationAction.Bench, result.Action);
        Assert.Equal(0m, result.ProjectedImpact);
        Assert.Null(result.RecommendedReplacement);
    }

    [Fact]
    public void EvaluateHoldsAvailablePlayerWithGoodFixturesAndLowGain()
    {
        var result = PlayerRecommendationPolicy.Evaluate(
            Availability("a", "Available", 95m),
            Fixtures("Favorable"),
            Transfers(Candidate(2m, 85m)));

        Assert.Equal(PlayerRecommendationAction.Hold, result.Action);
        Assert.Equal(0m, result.ProjectedImpact);
        Assert.Contains("no legal candidate provides a sufficient projected gain", result.Reason);
    }

    [Fact]
    public void EvaluateTransfersAvailablePlayerForStrongProjectedUpgrade()
    {
        var result = PlayerRecommendationPolicy.Evaluate(
            Availability("a", "Available", 95m),
            Fixtures("Favorable"),
            Transfers(Candidate(5m, 88m)));

        Assert.Equal(PlayerRecommendationAction.Transfer, result.Action);
        Assert.Equal(5m, result.ProjectedImpact);
    }

    [Fact]
    public void EvaluateRejectsSupportingDataForDifferentPlayers()
    {
        var fixtures = Fixtures("Mixed") with
        {
            Player = new CoachFixturePlayer(999, "Other", "Chelsea", "MID")
        };

        Assert.Throws<InvalidOperationException>(() => PlayerRecommendationPolicy.Evaluate(
            Availability("a", "Available", 95m),
            fixtures,
            Transfers(Candidate(5m, 80m))));
    }

    [Fact]
    public async Task ServiceCombinesAllThreeVerifiedFactResults()
    {
        var facts = new RecordingFactService();
        var service = new PlayerRecommendationService(facts);

        var result = await service.GetRecommendationAsync(Context(), 10, 4, 2, CancellationToken.None);

        Assert.Equal(PlayerRecommendationAction.Transfer, result.Action);
        Assert.Equal((10, 4), facts.FixtureRequest);
        Assert.Equal((10, 2), facts.TransferRequest);
        Assert.Equal(10, facts.AvailabilityPlayerId);
        Assert.Same(facts.AvailabilityResult, result.Availability);
        Assert.Same(facts.FixtureResult, result.Fixtures);
        Assert.Same(facts.TransferResult, result.Transfers);
    }

    [Fact]
    public async Task ServiceStopsAfterVerifiedAvailabilityWhenPlayerIsNotAtRisk()
    {
        var facts = new RecordingFactService(Availability("a", "Available", 95m));
        var service = new PlayerRecommendationService(facts);

        var result = await service.GetRecommendationIfAtRiskAsync(
            Context(),
            facts.AvailabilityResult,
            5,
            3,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Null(facts.FixtureRequest);
        Assert.Null(facts.TransferRequest);
    }

    [Fact]
    public async Task ServiceRunsIndependentFixtureAndTransferWorkConcurrentlyForRiskyPlayer()
    {
        var facts = new CoordinatedFactService();
        var service = new PlayerRecommendationService(facts);

        var recommendationTask = service.GetRecommendationIfAtRiskAsync(
            Context(),
            facts.AvailabilityResult,
            5,
            3,
            CancellationToken.None);
        await facts.BothSpecialistsStarted;
        Assert.False(recommendationTask.IsCompleted);

        facts.ReleaseSpecialists();
        var result = await recommendationTask;

        Assert.NotNull(result);
        Assert.Equal(PlayerRecommendationAction.Transfer, result.Action);
    }

    private static PlayerAvailabilityResult Availability(string status, string description, decimal confidence) => new(
        new CoachAvailabilityPlayer(10, "Saka", "Arsenal", "MID"),
        status,
        description,
        status == "a",
        status == "d" ? 50 : status == "a" ? 100 : 0,
        null,
        confidence,
        "Verified FPL status",
        "Official FPL bootstrap data");

    private static PlayerFixtureWindowResult Fixtures(string rating) => new(
        new CoachFixturePlayer(10, "Saka", "Arsenal", "MID"),
        5,
        [new CoachUpcomingFixture(1, 9, "Gameweek 9", null, "Chelsea", true, "Home", rating == "Difficult" ? 5 : 2)],
        rating == "Difficult" ? 5m : 2m,
        rating == "Difficult" ? 1m : 4m,
        rating,
        $"{rating} schedule.",
        "Official FPL fixture data");

    private static PlayerReplacementResult Transfers(params CoachReplacementCandidate[] candidates) => new(
        new CoachTransferPlayer(10, "Saka", "Arsenal", "MID", 10m),
        0.5m,
        10.5m,
        5,
        candidates,
        "C#-validated transfer engine");

    private static CoachReplacementCandidate Candidate(decimal gain, decimal confidence) => new(
        1,
        new CoachTransferPlayer(20, "Palmer", "Chelsea", "MID", 9.5m),
        -0.5m,
        25m,
        25m + gain,
        gain,
        confidence,
        "Deterministic projected-points upgrade.");

    private static FplCoachContext Context() => new(
        42,
        "Expected Goals",
        8,
        0.5m,
        100m,
        [new FplCoachSquadPlayer(10, "Saka", "Arsenal", "MID", 10m, "i", "Injured", 0, true, false, false)]);

    private sealed class RecordingFactService(
        PlayerAvailabilityResult? availabilityResult = null) : IFplCoachFactService
    {
        public int? AvailabilityPlayerId { get; private set; }
        public (int PlayerId, int Gameweeks)? FixtureRequest { get; private set; }
        public (int PlayerId, int Limit)? TransferRequest { get; private set; }
        public PlayerAvailabilityResult AvailabilityResult { get; } = availabilityResult ?? Availability("i", "Injured", 95m);
        public PlayerFixtureWindowResult FixtureResult { get; } = Fixtures("Mixed");
        public PlayerReplacementResult TransferResult { get; } = Transfers(Candidate(4m, 82m));

        public PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId)
        {
            AvailabilityPlayerId = playerId;
            return AvailabilityResult;
        }

        public Task<PlayerFixtureWindowResult> GetUpcomingFixturesAsync(FplCoachContext context, int playerId, int gameweeks, CancellationToken cancellationToken)
        {
            FixtureRequest = (playerId, gameweeks);
            return Task.FromResult(FixtureResult);
        }

        public Task<PlayerReplacementResult> GetTransferCandidatesAsync(FplCoachContext context, int playerId, int limit, CancellationToken cancellationToken)
        {
            TransferRequest = (playerId, limit);
            return Task.FromResult(TransferResult);
        }
    }

    private sealed class CoordinatedFactService : IFplCoachFactService
    {
        private readonly TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int startedCount;

        public PlayerAvailabilityResult AvailabilityResult { get; } = Availability("i", "Injured", 95m);
        public Task BothSpecialistsStarted => bothStarted.Task;

        public void ReleaseSpecialists() => release.SetResult();

        public PlayerAvailabilityResult GetPlayerAvailability(FplCoachContext context, int playerId) => AvailabilityResult;

        public async Task<PlayerFixtureWindowResult> GetUpcomingFixturesAsync(
            FplCoachContext context,
            int playerId,
            int gameweeks,
            CancellationToken cancellationToken)
        {
            SignalStarted();
            await release.Task.WaitAsync(cancellationToken);
            return Fixtures("Mixed");
        }

        public async Task<PlayerReplacementResult> GetTransferCandidatesAsync(
            FplCoachContext context,
            int playerId,
            int limit,
            CancellationToken cancellationToken)
        {
            SignalStarted();
            await release.Task.WaitAsync(cancellationToken);
            return Transfers(Candidate(4m, 82m));
        }

        private void SignalStarted()
        {
            if (Interlocked.Increment(ref startedCount) == 2)
            {
                bothStarted.SetResult();
            }
        }
    }
}