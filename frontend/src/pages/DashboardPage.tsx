import { ArrowRight, CalendarDays, TrendingUp } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Card, CardHeader } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { fixtures, players } from '../data/placeholderData'

const stats = [
  { label: 'Gameweek points', value: '67', note: '18 above average', accent: 'bg-[#b8ff3d]' },
  { label: 'Overall rank', value: '184k', note: 'Up 32,510 places', accent: 'bg-[#ff795f]' },
  { label: 'Squad value', value: '£101.2m', note: '£1.5m in bank', accent: 'bg-[#77d6c5]' },
  { label: 'Free transfers', value: '2', note: 'Deadline Friday', accent: 'bg-[#e9b5ff]' },
]

export function DashboardPage() {
  return (
    <>
      <PageHeader eyebrow="Gameweek 1" title="Good morning, manager." description="Your squad is settled. Two fixture swings are worth watching before Friday's deadline." />

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
          <CardHeader title="Squad pulse" detail="Highest projected performers" action={<TrendingUp size={18} className="text-[#287c50]" />} />
          <div className="divide-y divide-black/8">
            {players.slice(0, 4).map((player, index) => (
              <div key={player.id} className="grid grid-cols-[2rem_1fr_auto_auto] items-center gap-3 px-5 py-4">
                <span className="font-display text-lg font-bold text-black/25">0{index + 1}</span>
                <div>
                  <p className="text-sm font-bold">{player.name}</p>
                  <p className="mt-1 text-xs text-black/45">{player.team} · {player.position}</p>
                </div>
                <span className="text-xs font-semibold text-[#287c50]">Form {player.form}</span>
                <span className="min-w-12 text-right font-display text-xl font-bold">{player.points}</span>
              </div>
            ))}
          </div>
          <Link to="/players" className="flex h-12 items-center justify-center gap-2 border-t border-black/10 text-sm font-bold text-[#287c50]">
            Explore players <ArrowRight size={16} />
          </Link>
        </Card>

        <Card>
          <CardHeader title="Next up" detail="Opening fixtures" action={<CalendarDays size={18} />} />
          <div className="divide-y divide-black/8">
            {fixtures.slice(0, 3).map((fixture) => (
              <div key={fixture.id} className="px-5 py-4">
                <p className="text-xs text-black/45">{fixture.day} · {fixture.time}</p>
                <div className="mt-3 flex items-center justify-between text-sm font-bold">
                  <span>{fixture.home}</span><span className="text-black/25">vs</span><span>{fixture.away}</span>
                </div>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </>
  )
}