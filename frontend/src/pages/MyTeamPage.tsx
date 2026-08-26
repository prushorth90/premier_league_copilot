import { Save } from 'lucide-react'
import { SquadPitch } from '../components/team/SquadPitch'
import { Card, CardHeader } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'

export function MyTeamPage() {
  return (
    <>
      <PageHeader
        eyebrow="North Bank XI"
        title="My Team"
        description="Set your shape, captaincy, and bench order for the upcoming gameweek."
        action={<button className="inline-flex h-11 items-center justify-center gap-2 bg-[#151a17] px-5 text-sm font-bold text-white"><Save size={17} /> Save lineup</button>}
      />
      <div className="grid gap-7 xl:grid-cols-[minmax(0,1.5fr)_22rem]">
        <SquadPitch />
        <div className="space-y-7">
          <Card>
            <CardHeader title="Team summary" detail="Placeholder gameweek data" />
            <dl className="grid grid-cols-2 gap-px bg-black/8">
              {[['Projected', '71.4'], ['Cost', '£99.7m'], ['Bank', '£1.5m'], ['Formation', '4-4-2']].map(([label, value]) => (
                <div key={label} className="bg-white p-4"><dt className="text-xs text-black/45">{label}</dt><dd className="mt-2 font-display text-xl font-bold">{value}</dd></div>
              ))}
            </dl>
          </Card>
          <Card>
            <CardHeader title="Bench" detail="Order matters for auto-subs" />
            <div className="divide-y divide-black/8">
              {['Areola · GKP', 'Andersen · DEF', 'Rogers · MID', 'João Pedro · FWD'].map((item, index) => (
                <div key={item} className="flex items-center gap-3 px-5 py-3 text-sm"><span className="grid size-6 place-items-center bg-[#f4f4ef] text-xs font-bold">{index + 1}</span><span className="font-semibold">{item}</span></div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </>
  )
}