import { Search, SlidersHorizontal } from 'lucide-react'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { players } from '../data/placeholderData'

export function PlayersPage() {
  return (
    <>
      <PageHeader eyebrow="612 available" title="Players" description="Scan form, value, and points across the player pool." />
      <div className="mb-5 flex flex-col gap-3 sm:flex-row">
        <label className="flex h-11 flex-1 items-center gap-3 border border-black/15 bg-white px-4"><Search size={17} className="text-black/35" /><input aria-label="Search players" placeholder="Search player or club" className="w-full bg-transparent text-sm outline-none" /></label>
        <button className="inline-flex h-11 items-center justify-center gap-2 border border-black/15 bg-white px-4 text-sm font-bold"><SlidersHorizontal size={17} /> Filters</button>
      </div>
      <Card>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="bg-[#151a17] text-xs text-white/60"><tr><th className="px-5 py-4">Player</th><th>Position</th><th>Price</th><th>Form</th><th>Total</th><th className="px-5 text-right">Value index</th></tr></thead>
            <tbody className="divide-y divide-black/8">{players.map((player, index) => <tr key={player.id} className="hover:bg-[#f4f4ef]"><td className="px-5 py-4"><div className="flex items-center gap-3"><span className={`grid size-9 place-items-center rounded-full text-[10px] font-bold ${index % 2 ? 'bg-[#77d6c5]' : 'bg-[#b8ff3d]'}`}>{player.team.slice(0, 3).toUpperCase()}</span><div><p className="font-bold">{player.name}</p><p className="text-xs text-black/45">{player.team}</p></div></div></td><td>{player.position}</td><td>{player.price}</td><td><span className="bg-[#e5f6ef] px-2 py-1 font-bold text-[#287c50]">{player.form}</span></td><td className="font-display text-lg font-bold">{player.points}</td><td className="px-5 text-right font-bold">{(player.points / Number(player.price.slice(1, -1))).toFixed(1)}</td></tr>)}</tbody>
          </table>
        </div>
      </Card>
    </>
  )
}