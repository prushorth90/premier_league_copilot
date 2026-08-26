import { Crown, RefreshCw, ShieldCheck, Sparkles } from 'lucide-react'
import { Card, CardHeader } from '../components/ui/Card'
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/ui/States'
import { PageHeader } from '../components/ui/PageHeader'
import type { CaptainCandidate } from '../models/fpl'
import { useCaptainRecommendationQuery } from '../queries/fplQueries'
import { useTeam } from '../team/useTeam'

export function RecommendationsPage() {
  const { teamId } = useTeam()
  const recommendationQuery = useCaptainRecommendationQuery(teamId)

  if (recommendationQuery.isPending) {
    return (
      <>
        <PageHeader eyebrow="Captaincy" title="Analysing your starting XI" description="Comparing projections, minutes, fixtures, attacking threat, and availability." />
        <div className="grid gap-7 lg:grid-cols-2"><Card><LoadingSkeleton rows={5} /></Card><Card><LoadingSkeleton rows={5} /></Card></div>
      </>
    )
  }

  if (recommendationQuery.isError || !recommendationQuery.data) {
    return (
      <>
        <PageHeader eyebrow="Captaincy" title="Recommendation unavailable" description="Your saved team remains connected." />
        <ErrorState
          description={recommendationQuery.error instanceof Error ? recommendationQuery.error.message : 'The recommendation response was incomplete.'}
          action={<button onClick={() => void recommendationQuery.refetch()} className="inline-flex h-10 items-center gap-2 bg-[#151a17] px-4 text-sm font-bold text-white"><RefreshCw size={16} /> Try again</button>}
        />
      </>
    )
  }

  const recommendation = recommendationQuery.data

  return (
    <>
      <PageHeader
        eyebrow={`Gameweek ${recommendation.gameweek}`}
        title="Captain recommendation"
        description="A deterministic ranking of your starting XI with every scoring factor explained."
        action={<button onClick={() => void recommendationQuery.refetch()} disabled={recommendationQuery.isFetching} className="inline-flex h-11 items-center gap-2 border border-black/15 bg-white px-4 text-sm font-bold disabled:opacity-60"><RefreshCw className={recommendationQuery.isFetching ? 'animate-spin' : ''} size={16} /> Refresh</button>}
      />

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