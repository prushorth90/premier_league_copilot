import { ArrowDownToLine, ArrowUpFromLine, Clock3, Crown, RefreshCw, ShieldCheck, Sparkles, UsersRound } from 'lucide-react'
import { Card, CardHeader } from '../components/ui/Card'
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/ui/States'
import { PageHeader } from '../components/ui/PageHeader'
import type { CaptainCandidate, LineupPlayer } from '../models/fpl'
import { useCaptainRecommendationQuery, useLineupRecommendationQuery } from '../queries/fplQueries'
import { useTeam } from '../team/useTeam'

export function RecommendationsPage() {
  const { teamId } = useTeam()
  const recommendationQuery = useCaptainRecommendationQuery(teamId)
  const lineupQuery = useLineupRecommendationQuery(teamId)

  if (recommendationQuery.isPending || lineupQuery.isPending) {
    return (
      <>
        <PageHeader eyebrow="Decision support" title="Optimising your squad" description="Evaluating every legal formation, projected points, expected minutes, and captaincy option." />
        <div className="grid gap-7 lg:grid-cols-2"><Card><LoadingSkeleton rows={5} /></Card><Card><LoadingSkeleton rows={5} /></Card></div>
      </>
    )
  }

  if (recommendationQuery.isError || lineupQuery.isError || !recommendationQuery.data || !lineupQuery.data) {
    const error = recommendationQuery.error ?? lineupQuery.error
    return (
      <>
        <PageHeader eyebrow="Decision support" title="Recommendations unavailable" description="Your saved team remains connected." />
        <ErrorState
          description={error instanceof Error ? error.message : 'The recommendation response was incomplete.'}
          action={<button onClick={() => { void recommendationQuery.refetch(); void lineupQuery.refetch() }} className="inline-flex h-10 items-center gap-2 bg-[#151a17] px-4 text-sm font-bold text-white"><RefreshCw size={16} /> Try again</button>}
        />
      </>
    )
  }

  const recommendation = recommendationQuery.data
  const lineup = lineupQuery.data
  const isFetching = recommendationQuery.isFetching || lineupQuery.isFetching

  return (
    <>
      <PageHeader
        eyebrow={`Gameweek ${recommendation.gameweek}`}
        title="Squad recommendations"
        description="Your strongest legal lineup, bench order, and captaincy ranking for the next fixture."
        action={<button onClick={() => { void recommendationQuery.refetch(); void lineupQuery.refetch() }} disabled={isFetching} className="inline-flex h-11 items-center gap-2 border border-black/15 bg-white px-4 text-sm font-bold disabled:opacity-60"><RefreshCw className={isFetching ? 'animate-spin' : ''} size={16} /> Refresh</button>}
      />

      <div className="grid gap-7 xl:grid-cols-[1.45fr_0.75fr]">
        <Card>
          <CardHeader title="Recommended starting XI" detail="Projected points weighted by expected minutes" action={<span className="font-display shrink-0 whitespace-nowrap bg-[#b8ff3d] px-3 py-1.5 text-lg font-bold">{lineup.formation}</span>} />
          <div className="divide-y divide-black/8">
            {(['GKP', 'DEF', 'MID', 'FWD'] as const).map((position) => (
              <LineupRow key={position} position={position} players={lineup.startingXi.filter((player) => player.position === position)} />
            ))}
          </div>
        </Card>

        <div className="space-y-7">
          <Card>
            <CardHeader title="Bench order" detail="Outfield substitution priority" action={<UsersRound size={18} className="text-[#287c50]" />} />
            <div className="divide-y divide-black/8">
              {lineup.bench.map((player, index) => <BenchPlayer key={player.playerId} player={player} order={index + 1} />)}
            </div>
          </Card>
          <Card>
            <CardHeader title="Lineup changes" detail="Compared with your current XI" />
            {lineup.changes.length === 0 ? (
              <EmptyState title="Current XI retained" description="Your existing lineup already matches the recommendation." />
            ) : (
              <div className="divide-y divide-black/8">
                {lineup.changes.map((change) => {
                  const movedIn = change.changeType === 'Moved to starting XI'
                  const Icon = movedIn ? ArrowUpFromLine : ArrowDownToLine
                  return <div key={change.playerId} className="flex items-center gap-3 px-5 py-4"><span className={`grid size-8 place-items-center ${movedIn ? 'bg-[#b8ff3d]' : 'bg-[#ffcec5]'}`}><Icon size={15} /></span><div><p className="text-sm font-bold">{change.playerName}</p><p className="mt-0.5 text-xs text-black/45">{change.changeType}</p></div></div>
                })}
              </div>
            )}
          </Card>
        </div>
      </div>

      <div className="my-7 flex items-center gap-3"><span className="h-px flex-1 bg-black/10" /><span className="text-[10px] font-bold uppercase text-black/35">Captaincy</span><span className="h-px flex-1 bg-black/10" /></div>

      <div className="grid gap-7 lg:grid-cols-2">
        <CandidateCard candidate={recommendation.bestCaptain} role="Captain" icon={Crown} accent="bg-[#b8ff3d]" />
        <CandidateCard candidate={recommendation.viceCaptain} role="Vice captain" icon={ShieldCheck} accent="bg-[#77d6c5]" />
      </div>

      <div className="mt-7 grid gap-7 xl:grid-cols-[1.25fr_1fr]">
        <Card>
          <CardHeader title="Why this captain?" detail={recommendation.bestCaptain.playerName} action={<Sparkles size={18} className="text-[#287c50]" />} />
          <FactorBreakdown candidate={recommendation.bestCaptain} />
        </Card>
        <Card>
          <CardHeader title="Alternatives" detail="Next best starting players" />
          {recommendation.alternatives.length === 0 ? (
            <EmptyState title="No alternatives available" description="At least three starting players are needed." />
          ) : (
            <div className="divide-y divide-black/8">
              {recommendation.alternatives.map((candidate, index) => (
                <article key={candidate.playerId} className="grid grid-cols-[2rem_1fr_auto] items-center gap-3 px-5 py-4">
                  <span className="font-display text-xl font-bold text-black/20">{index + 3}</span>
                  <div className="min-w-0"><h3 className="truncate text-sm font-bold">{candidate.playerName}</h3><p className="mt-1 text-xs text-black/45">{candidate.teamName} · {candidate.position}</p></div>
                  <div className="text-right"><p className="font-display text-xl font-bold">{candidate.projectedPoints.toFixed(2)}</p><p className="text-[9px] font-bold uppercase text-black/35">projected</p></div>
                </article>
              ))}
            </div>
          )}
        </Card>
      </div>
    </>
  )
}

function LineupRow({ position, players }: { position: string; players: LineupPlayer[] }) {
  return (
    <section aria-label={position} className="grid gap-3 p-5 sm:grid-cols-[3.25rem_1fr] sm:items-start">
      <span className="pt-3 text-[10px] font-bold uppercase text-black/35">{position}</span>
      <div className="grid gap-px bg-black/8 sm:grid-cols-2">
        {players.map((player) => <LineupPlayerCell key={player.playerId} player={player} />)}
      </div>
    </section>
  )
}

function LineupPlayerCell({ player }: { player: LineupPlayer }) {
  return (
    <div className="flex min-h-24 items-center justify-between gap-3 bg-white p-4">
      <div className="min-w-0"><p className="truncate text-sm font-bold">{player.playerName}</p><p className="mt-1 truncate text-xs text-black/45">{player.teamName}</p><p className="mt-2 flex items-center gap-1 text-[10px] text-black/40"><Clock3 size={12} /> {player.expectedMinutes.toFixed(0)} min</p></div>
      <div className="text-right"><p className="font-display text-xl font-bold">{player.projectedPoints.toFixed(2)}</p><p className="text-[9px] font-bold uppercase text-black/35">projected</p></div>
    </div>
  )
}

function BenchPlayer({ player, order }: { player: LineupPlayer; order: number }) {
  return (
    <div className="grid grid-cols-[2rem_1fr_auto] items-center gap-3 px-5 py-4">
      <span className="font-display text-xl font-bold text-black/20">{order}</span>
      <div className="min-w-0"><p className="truncate text-sm font-bold">{player.playerName}</p><p className="mt-1 text-xs text-black/45">{player.position} · {player.expectedMinutes.toFixed(0)} min</p></div>
      <p className="font-display text-lg font-bold">{player.projectedPoints.toFixed(2)}</p>
    </div>
  )
}

function CandidateCard({ candidate, role, icon: Icon, accent }: { candidate: CaptainCandidate; role: string; icon: typeof Crown; accent: string }) {
  return (
    <Card className="relative overflow-hidden">
      <span className={`absolute inset-y-0 left-0 w-2 ${accent}`} />
      <div className="flex items-start justify-between gap-5 p-6 sm:p-8">
        <div><div className="flex items-center gap-2 text-xs font-bold uppercase text-[#287c50]"><Icon size={16} /> {role}</div><h2 className="font-display mt-4 text-3xl font-bold">{candidate.playerName}</h2><p className="mt-2 text-sm text-black/45">{candidate.teamName} · {candidate.position}</p></div>
        <div className="text-right"><p className="font-display text-4xl font-bold">{candidate.projectedPoints.toFixed(2)}</p><p className="mt-1 text-[10px] font-bold uppercase text-black/35">Projected points</p><p className="mt-4 text-xs text-black/45">Rank score <strong className="text-[#151a17]">{candidate.rankingScore.toFixed(2)}</strong></p></div>
      </div>
    </Card>
  )
}

function FactorBreakdown({ candidate }: { candidate: CaptainCandidate }) {
  return (
    <div className="divide-y divide-black/8">
      {candidate.factors.map((factor) => (
        <article key={factor.factor} className="grid grid-cols-[1fr_auto] gap-4 px-5 py-4">
          <div><h3 className="text-sm font-bold">{factor.factor}</h3><p className="mt-1 text-xs leading-5 text-black/45">{factor.explanation}</p></div>
          <span className={`font-display text-lg font-bold ${factor.score >= 0 ? 'text-[#287c50]' : 'text-[#a63625]'}`}>{factor.score >= 0 ? '+' : ''}{factor.score.toFixed(2)}</span>
        </article>
      ))}
    </div>
  )
}