import { useQuery } from '@tanstack/react-query'
import { getFixtures, getPlayers, getSquad, getTeam } from '../api/fplApi'

const minute = 60_000

export const fplQueryKeys = {
  all: ['fpl'] as const,
  team: (teamId: number | null) => [...fplQueryKeys.all, 'team', teamId] as const,
  squad: (teamId: number | null) => [...fplQueryKeys.all, 'squad', teamId] as const,
  players: () => [...fplQueryKeys.all, 'players'] as const,
  fixtures: () => [...fplQueryKeys.all, 'fixtures'] as const,
}

export function useFplTeamQuery(teamId: number | null) {
  return useQuery({
    queryKey: fplQueryKeys.team(teamId),
    queryFn: ({ signal }) => getTeam(teamId!, signal),
    enabled: teamId !== null,
    staleTime: 5 * minute,
  })
}

export function useFplSquadQuery(teamId: number | null) {
  return useQuery({
    queryKey: fplQueryKeys.squad(teamId),
    queryFn: ({ signal }) => getSquad(teamId!, signal),
    enabled: teamId !== null,
    staleTime: 5 * minute,
  })
}

export function useFplPlayersQuery() {
  return useQuery({
    queryKey: fplQueryKeys.players(),
    queryFn: ({ signal }) => getPlayers(signal),
    staleTime: 60 * minute,
  })
}

export function useFplFixturesQuery() {
  return useQuery({
    queryKey: fplQueryKeys.fixtures(),
    queryFn: ({ signal }) => getFixtures(signal),
    staleTime: 15 * minute,
  })
}