import { ArrowRight, CalendarDays, RefreshCw, TrendingUp } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Card, CardHeader } from '../components/ui/Card'
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/ui/States'
import { PageHeader } from '../components/ui/PageHeader'
import { useFplFixturesQuery, useFplPlayersQuery, useFplSquadQuery, useFplTeamQuery } from '../queries/fplQueries'
import { useTeam } from '../team/useTeam'

const rankFormatter = new Intl.NumberFormat('en-GB', { notation: 'compact', maximumFractionDigits: 1 })
const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

export function DashboardPage() {
  const { teamId } = useTeam()
  const teamQuery = useFplTeamQuery(teamId)
  const squadQuery = useFplSquadQuery(teamId)
  const playersQuery = useFplPlayersQuery()
  const fixturesQuery = useFplFixturesQuery()
  const queries = [teamQuery, squadQuery, playersQuery, fixturesQuery]
  const isLoading = queries.some((query) => query.isPending)
  const isFetching = queries.some((query) => query.isFetching)
  const failedQuery = queries.find((query) => query.isError)

  function refetchDashboard() {
    void Promise.all(queries.map((query) => query.refetch()))
  }

  if (isLoading) {
    return (
      <>
        <PageHeader eyebrow="Connecting team" title="Loading dashboard" description="Fetching your latest squad, players, and fixtures." />
        <div className="grid gap-7 lg:grid-cols-2">
          <Card><LoadingSkeleton rows={4} /></Card>
          <Card><LoadingSkeleton rows={4} /></Card>
        </div>
      </>
    )
  }

  if (failedQuery || !teamQuery.data || !squadQuery.data || !playersQuery.data || !fixturesQuery.data) {
    return (
      <>
        <PageHeader eyebrow="Dashboard" title="Your team data is unavailable" description="The saved team ID is still connected." />
        <ErrorState
          description={failedQuery?.error instanceof Error ? failedQuery.error.message : 'The dashboard response was incomplete.'}
          action={<button onClick={refetchDashboard} className="inline-flex h-10 items-center gap-2 bg-[#151a17] px-4 text-sm font-bold text-white"><RefreshCw size={16} /> Try again</button>}
        />
      </>
    )
  }

  const team = teamQuery.data
  const squad = squadQuery.data
  const topPlayers = [...playersQuery.data]
    .sort((left, right) => right.totalPoints - left.totalPoints)
    .slice(0, 4)
  const upcomingFixtures = fixturesQuery.data
    .filter((fixture) => !fixture.finished)
    .sort((left, right) => (left.kickoff ?? '').localeCompare(right.kickoff ?? ''))
    .slice(0, 3)
  const firstName = team.managerName.split(' ')[0]
  const stats = [
    { label: 'Gameweek points', value: String(team.gameweekPoints), note: `${squad.summary.benchPoints} points on bench`, accent: 'bg-[#b8ff3d]' },
    { label: 'Overall rank', value: team.overallRank ? rankFormatter.format(team.overallRank) : 'Unranked', note: `${team.overallPoints} total points`, accent: 'bg-[#ff795f]' },
    { label: 'Squad value', value: `£${squad.summary.teamValue.toFixed(1)}m`, note: `£${squad.summary.bank.toFixed(1)}m in bank`, accent: 'bg-[#77d6c5]' },
    { label: 'Gameweek transfers', value: String(squad.summary.transfers), note: `${squad.summary.transferCost} point cost`, accent: 'bg-[#e9b5ff]' },
  ]

  return (
    <>
      <PageHeader
        eyebrow={`Gameweek ${team.currentGameweek}`}
        title={`Good morning, ${firstName}.`}
        description={`${team.teamName} is connected and showing the latest available FPL data.`}
        action={
          <button onClick={refetchDashboard} disabled={isFetching} className="inline-flex h-11 items-center justify-center gap-2 border border-black/15 bg-white px-4 text-sm font-bold disabled:cursor-wait disabled:opacity-60">
            <RefreshCw className={isFetching ? 'animate-spin' : ''} size={17} />
            {isFetching ? 'Refreshing' : 'Refresh'}
          </button>
        }
      />

      <div className="grid gap-px border border-black/10 bg-black/10 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => (
          <div key={stat.label} className="relative bg-white p-5">
            <span className={`absolute inset-y-0 left-0 w-1 ${stat.accent}`} />
            <p className="text-xs font-bold uppercase text-black/45">{stat.label}</p>
            <p className="font-display mt-3 text-3xl font-bold">{stat.value}</p>
            <p className="mt-2 text-xs text-black/45">{stat.note}</p>
          </div>
        ))}
      </div>

      <div className="mt-7 grid gap-7 xl:grid-cols-[1.5fr_1fr]">
        <Card>
          <CardHeader title="Player leaders" detail="Highest total points" action={<TrendingUp size={18} className="text-[#287c50]" />} />
          {topPlayers.length === 0 ? (
            <EmptyState title="No players available" description="Player data has not been published yet." />
          ) : (
            <div className="divide-y divide-black/8">
            {topPlayers.map((player, index) => (
              <div key={player.id} className="grid grid-cols-[2rem_1fr_auto_auto] items-center gap-3 px-5 py-4">
                <span className="font-display text-lg font-bold text-black/25">0{index + 1}</span>
                <div>
                  <p className="text-sm font-bold">{player.displayName}</p>
                  <p className="mt-1 text-xs text-black/45">{player.teamName} · {player.position}</p>
                </div>
                <span className="text-xs font-semibold text-[#287c50]">£{player.price.toFixed(1)}m</span>
                <span className="min-w-12 text-right font-display text-xl font-bold">{player.totalPoints}</span>
              </div>
            ))}
            </div>
          )}
          <Link to="/players" className="flex h-12 items-center justify-center gap-2 border-t border-black/10 text-sm font-bold text-[#287c50]">
            Explore players <ArrowRight size={16} />
          </Link>
        </Card>

        <Card>
          <CardHeader title="Next up" detail="Upcoming fixtures" action={<CalendarDays size={18} />} />
          {upcomingFixtures.length === 0 ? (
            <EmptyState title="No upcoming fixtures" description="The current fixture list is complete." />
          ) : (
            <div className="divide-y divide-black/8">
            {upcomingFixtures.map((fixture) => (
              <div key={fixture.id} className="px-5 py-4">
                <p className="text-xs text-black/45">{fixture.kickoff ? dateFormatter.format(new Date(fixture.kickoff)) : 'Date to be confirmed'}</p>
                <div className="mt-3 grid grid-cols-[1fr_auto_1fr] items-center gap-3 text-sm font-bold">
                  <span>{fixture.homeTeam}</span><span className="text-black/25">vs</span><span className="text-right">{fixture.awayTeam}</span>
                </div>
              </div>
            ))}
            </div>
          )}
        </Card>
      </div>
    </>
  )
}