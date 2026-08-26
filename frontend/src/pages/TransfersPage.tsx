import { ArrowRight, Search, X } from 'lucide-react'
import { Card, CardHeader } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { players } from '../data/placeholderData'

export function TransfersPage() {
  return (
    <>
      <PageHeader eyebrow="2 free transfers" title="Transfers" description="Compare exits and targets while staying inside your £1.5m budget." />
      <div className="mb-7 grid gap-px border border-black/10 bg-black/10 sm:grid-cols-3">
        {[['Bank', '£1.5m'], ['Squad cost', '£99.7m'], ['Points cost', '0 pts']].map(([label, value]) => (
          <div key={label} className="bg-white px-5 py-4"><p className="text-xs text-black/45">{label}</p><p className="mt-1 font-display text-2xl font-bold">{value}</p></div>
        ))}
      </div>
      <div className="grid gap-7 xl:grid-cols-[1fr_auto_1fr]">
        <Card>
          <CardHeader title="Transfer out" detail="Current squad" />
          <div className="flex items-center gap-4 p-5"><span className="grid size-12 place-items-center rounded-full bg-[#ff795f] font-bold">MID</span><div className="flex-1"><p className="font-bold">B. Fernandes</p><p className="text-xs text-black/45">Man Utd · £8.5m</p></div><button title="Remove transfer" className="text-black/35"><X size={18} /></button></div>
        </Card>
        <div className="hidden place-items-center xl:grid"><ArrowRight /></div>
        <Card>
          <CardHeader title="Transfer in" detail="Selected target" />
          <div className="flex items-center gap-4 p-5"><span className="grid size-12 place-items-center rounded-full bg-[#b8ff3d] text-xs font-bold">MID</span><div className="flex-1"><p className="font-bold">C. Palmer</p><p className="text-xs text-black/45">Chelsea · £10.5m</p></div><span className="text-sm font-bold text-[#287c50]">+2.1 proj.</span></div>
        </Card>
      </div>
      <Card className="mt-7">
        <CardHeader title="Player market" detail="Placeholder comparison data" action={<Search size={18} />} />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="bg-[#f4f4ef] text-xs text-black/45"><tr><th className="px-5 py-3">Player</th><th>Position</th><th>Price</th><th>Form</th><th>Points</th><th className="px-5 text-right">Action</th></tr></thead>
            <tbody className="divide-y divide-black/8">{players.map((player) => <tr key={player.id}><td className="px-5 py-4"><p className="font-bold">{player.name}</p><p className="text-xs text-black/45">{player.team}</p></td><td>{player.position}</td><td>{player.price}</td><td>{player.form}</td><td className="font-bold">{player.points}</td><td className="px-5 text-right"><button className="bg-[#151a17] px-3 py-2 text-xs font-bold text-white">Select</button></td></tr>)}</tbody>
          </table>
        </div>
      </Card>
    </>
  )
}