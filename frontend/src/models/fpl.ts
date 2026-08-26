export interface FplTeam {
  id: number
  managerName: string
  teamName: string
  startedGameweek: number
  currentGameweek: number
  overallPoints: number
  overallRank: number | null
  gameweekPoints: number
  gameweekRank: number | null
  bank: number
  teamValue: number
}

export interface FplPlayer {
  id: number
  code: number
  firstName: string
  lastName: string
  displayName: string
  teamId: number
  teamName: string
  positionId: number
  position: string
  price: number
  totalPoints: number
  gameweekPoints: number
  status: string
  news: string
  chanceOfPlayingNextRound: number | null
}

export interface FplFixture {
  id: number
  code: number
  gameweek: number | null
  kickoff: string | null
  finished: boolean
  started: boolean
  homeTeamId: number
  homeTeam: string
  awayTeamId: number
  awayTeam: string
  homeScore: number | null
  awayScore: number | null
  homeDifficulty: number
  awayDifficulty: number
}

export interface FplSquad {
  teamId: number
  teamName: string
  gameweek: number
  activeChip: string | null
  summary: FplSquadSummary
  picks: FplSquadPick[]
}

export interface FplSquadSummary {
  points: number
  totalPoints: number
  overallRank: number | null
  bank: number
  teamValue: number
  transfers: number
  transferCost: number
  benchPoints: number
}

export interface FplSquadPick {
  playerId: number
  displayName: string
  teamName: string
  positionName: string
  price: number
  squadPosition: number
  multiplier: number
  isCaptain: boolean
  isViceCaptain: boolean
}