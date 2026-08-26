using System.Net;
using System.Text;
using Backend.ExternalClients;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

public class FplApiClientTests
{
    [Fact]
    public async Task ClientDeserializesAllSupportedEndpoints()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/bootstrap-static/" => Json("""
                {"events":[{"id":1,"name":"Gameweek 1","deadline_time":"2026-08-21T19:00:00Z","finished":true,"is_current":true,"is_next":false,"average_entry_score":50,"highest_score":131}],"teams":[{"id":1,"code":3,"name":"Arsenal","short_name":"ARS","strength":4,"strength_overall_home":4,"strength_overall_away":5}],"element_types":[{"id":1,"singular_name":"Goalkeeper","singular_name_short":"GKP","squad_select":2,"squad_min_play":1,"squad_max_play":1}],"elements":[{"id":1,"code":154561,"first_name":"David","second_name":"Raya","web_name":"Raya","team":1,"element_type":1,"now_cost":60,"total_points":6,"event_points":6,"status":"a","news":"","chance_of_playing_next_round":null}]}
                """),
            "/api/fixtures/" => Json("""
                [{"id":1,"code":2645195,"event":1,"kickoff_time":"2026-08-21T19:00:00Z","finished":true,"started":true,"team_h":1,"team_a":7,"team_h_score":3,"team_a_score":0,"team_h_difficulty":2,"team_a_difficulty":4}]
                """),
            "/api/entry/1/" => Json("""
                {"id":1,"player_first_name":"Chris","player_last_name":"Musson","name":"Solio Moose","started_event":1,"current_event":1,"summary_overall_points":41,"summary_overall_rank":6875541,"summary_event_points":41,"summary_event_rank":6875552,"last_deadline_bank":0,"last_deadline_value":1000}
                """),
            "/api/entry/1/event/1/picks/" => Json("""
                {"active_chip":null,"entry_history":{"event":1,"points":41,"total_points":41,"overall_rank":6875541,"bank":0,"value":1000,"event_transfers":0,"event_transfers_cost":0,"points_on_bench":4},"picks":[{"element":1,"position":1,"multiplier":1,"is_captain":false,"is_vice_captain":false,"element_type":1}]}
                """),
            "/api/element-summary/1/" => Json("""
                {"fixtures":[{"id":20,"event":2,"event_name":"Gameweek 2","kickoff_time":"2026-08-31T19:00:00Z","is_home":false,"team_h":2,"team_a":1,"difficulty":4}],"history":[{"element":1,"fixture":1,"opponent_team":7,"round":1,"was_home":true,"kickoff_time":"2026-08-21T19:00:00Z","total_points":6,"minutes":90,"goals_scored":0,"assists":0,"clean_sheets":1,"goals_conceded":0,"bonus":0,"bps":24,"value":60,"selected":3347153,"transfers_in":0,"transfers_out":0}],"history_past":[{"season_name":"2025/26","element_code":154561,"start_cost":55,"end_cost":62,"total_points":162,"minutes":3330,"goals_scored":0,"assists":0,"clean_sheets":19,"goals_conceded":26,"bonus":11,"bps":633}]}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/api/")
        };
        var client = new FplApiClient(httpClient, NullLogger<FplApiClient>.Instance);

        var bootstrap = await client.GetBootstrapStaticAsync(CancellationToken.None);
        var fixtures = await client.GetFixturesAsync(CancellationToken.None);
        var manager = await client.GetManagerAsync(1, CancellationToken.None);
        var picks = await client.GetManagerPicksAsync(1, 1, CancellationToken.None);
        var history = await client.GetPlayerSummaryAsync(1, CancellationToken.None);

        Assert.Equal("Raya", Assert.Single(bootstrap.Elements).WebName);
        Assert.Equal(3, Assert.Single(fixtures).TeamHScore);
        Assert.Equal("Solio Moose", manager.Name);
        Assert.Equal(1, Assert.Single(picks.Picks).Element);
        Assert.Equal("2025/26", Assert.Single(history.HistoryPast).SeasonName);
    }

    [Fact]
    public async Task ClientWrapsUpstreamFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/") };
        var client = new FplApiClient(httpClient, NullLogger<FplApiClient>.Instance);

        var exception = await Assert.ThrowsAsync<FplApiException>(() =>
            client.GetFixturesAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("fixtures/", exception.Endpoint);
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}