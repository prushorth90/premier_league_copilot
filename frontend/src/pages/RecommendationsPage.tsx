import { ArrowRight, Sparkles } from 'lucide-react'
import { Card, CardHeader } from '../components/ui/Card'
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/ui/States'
import { PageHeader } from '../components/ui/PageHeader'

const ideas = [
  { title: 'Prioritise Palmer', label: 'Transfer', impact: '+2.1', detail: 'Strong home fixture and improving involvement.' },
  { title: 'Hold Gabriel', label: 'Squad', impact: '+1.4', detail: 'Clean-sheet run remains favourable for three weeks.' },
  { title: 'Captain Salah', label: 'Captaincy', impact: '+3.7', detail: 'Highest placeholder ceiling in the current squad.' },
]

export function RecommendationsPage() {
  return (
    <>
      <PageHeader eyebrow="Decision support" title="Recommendations" description="A structured queue for transfer, lineup, and captaincy ideas." />
      <div className="grid gap-7 xl:grid-cols-[1.3fr_1fr]">
        <Card>
          <CardHeader title="Priority actions" detail="Placeholder recommendations" action={<Sparkles size={18} className="text-[#287c50]" />} />
          <div className="divide-y divide-black/8">{ideas.map((idea, index) => <article key={idea.title} className="grid grid-cols-[auto_1fr_auto] items-start gap-4 p-5"><span className="font-display text-2xl font-bold text-black/20">0{index + 1}</span><div><span className="text-[10px] font-bold uppercase text-[#287c50]">{idea.label}</span><h3 className="mt-1 font-bold">{idea.title}</h3><p className="mt-2 text-sm text-black/50">{idea.detail}</p></div><div className="text-right"><p className="font-display text-xl font-bold text-[#287c50]">{idea.impact}</p><button title={`Review ${idea.title}`} className="mt-3 text-black/40"><ArrowRight size={18} /></button></div></article>)}</div>
        </Card>
        <div className="space-y-7">
          <Card><CardHeader title="Loading state" detail="Reusable skeleton" /><LoadingSkeleton rows={3} /></Card>
          <ErrorState title="Projection feed unavailable" description="Existing recommendations remain visible while projections reconnect." />
          <Card><EmptyState title="No watchlist alerts" description="Players you follow will appear here when their status changes." /></Card>
        </div>
      </div>
    </>
  )
}