import { squad } from '../../data/placeholderData'

export function SquadPitch() {
  return (
    <div className="pitch relative mx-auto aspect-[4/5] w-full max-w-2xl overflow-hidden bg-[#287c50]" aria-label="Squad formation">
      <div className="absolute inset-x-[7%] top-1/2 border-t border-white/30" />
      <div className="absolute left-1/2 top-1/2 size-24 -translate-x-1/2 -translate-y-1/2 rounded-full border border-white/30" />
      <div className="absolute inset-x-[22%] top-0 h-[12%] border-x border-b border-white/30" />
      <div className="absolute inset-x-[22%] bottom-0 h-[12%] border-x border-t border-white/30" />
      {squad.map((player) => (
        <div
          key={player.id}
          className="absolute -translate-x-1/2 -translate-y-1/2 text-center"
          style={{ left: `${player.x}%`, top: `${player.y}%` }}
        >
          <div className="relative mx-auto grid size-9 place-items-center rounded-full border-2 border-white bg-[#151a17] text-[10px] font-bold text-[#b8ff3d] shadow-lg sm:size-11 sm:text-xs">
            {player.role}
            {player.captain && (
              <span className="absolute -right-2 -top-2 grid size-5 place-items-center rounded-full bg-[#ff795f] text-[9px] text-[#151a17]">C</span>
            )}
          </div>
          <span className="mt-1 inline-block bg-white px-1.5 py-0.5 text-[9px] font-bold text-[#151a17] shadow sm:text-[11px]">
            {player.name}
          </span>
        </div>
      ))}
    </div>
  )
}