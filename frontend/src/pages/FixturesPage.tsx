import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { fixtures } from '../data/placeholderData'

export function FixturesPage() {
  return (
    <>
      <PageHeader eyebrow="Gameweek 2" title="Fixtures" description="Review the schedule and spot short-term fixture swings." action={<div className="flex gap-px bg-black/10"><button title="Previous gameweek" className="grid size-11 place-items-center bg-white"><ChevronLeft /></button><button title="Next gameweek" className="grid size-11 place-items-center bg-white"><ChevronRight /></button></div>} />
      <div className="grid gap-5 lg:grid-cols-2">
        {fixtures.map((fixture) => (
          <Card key={fixture.id} className="p-5">
            <div className="flex items-center justify-between text-xs text-black/45"><span>{fixture.day}</span><span>{fixture.time}</span></div>
            <div className="mt-6 grid grid-cols-[1fr_auto_1fr] items-center gap-4">
              <div className="text-center"><span className="mx-auto grid size-12 place-items-center bg-[#b8ff3d] text-xs font-bold">{fixture.homeCode}</span><p className="mt-3 text-sm font-bold">{fixture.home}</p></div>
              <span className="font-display text-lg font-bold text-black/25">VS</span>
              <div className="text-center"><span className="mx-auto grid size-12 place-items-center bg-[#77d6c5] text-xs font-bold">{fixture.awayCode}</span><p className="mt-3 text-sm font-bold">{fixture.away}</p></div>
            </div>
          </Card>
        ))}
      </div>
    </>
  )
}