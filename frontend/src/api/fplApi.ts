import type { CaptainRecommendation, FplFixture, FplPlayer, FplSquad, FplTeam, LineupRecommendation } from '../models/fpl'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5082'

export class TeamVerificationError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'TeamVerificationError'
  }
}

export class ApiError extends Error {
  readonly status: number | null

  constructor(message: string, status: number | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      headers: { Accept: 'application/json' },
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError('The backend is unavailable. Check your connection and try again.')
  }

  if (!response.ok) {
    throw new ApiError(
      response.status === 404
        ? 'The requested FPL data was not found.'
        : 'FPL data is temporarily unavailable. Try again shortly.',
      response.status,
    )
  }

  return response.json() as Promise<T>
}

export function getTeam(teamId: number, signal?: AbortSignal) {
  return request<FplTeam>(`/api/fpl/team/${teamId}`, signal)
}

export function getSquad(teamId: number, signal?: AbortSignal) {
  return request<FplSquad>(`/api/fpl/team/${teamId}/squad`, signal)
}

export function getPlayers(signal?: AbortSignal) {
  return request<FplPlayer[]>('/api/fpl/players', signal)
}

export function getFixtures(signal?: AbortSignal) {
  return request<FplFixture[]>('/api/fpl/fixtures', signal)
}

export function getCaptainRecommendation(teamId: number, signal?: AbortSignal) {
  return request<CaptainRecommendation>(`/api/recommendations/${teamId}/captain`, signal)
}

export function getLineupRecommendation(teamId: number, signal?: AbortSignal) {
  return request<LineupRecommendation>(`/api/recommendations/${teamId}/lineup`, signal)
}

export async function verifyTeam(teamId: number, signal?: AbortSignal): Promise<FplTeam> {
  try {
    return await getTeam(teamId, signal)
  } catch (error) {
    if (error instanceof ApiError) {
      throw new TeamVerificationError(
        error.status === 404
          ? 'We could not find that FPL team. Check the ID and try again.'
          : error.message,
      )
    }
    throw error
  }
}