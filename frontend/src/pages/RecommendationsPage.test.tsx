import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderToStaticMarkup } from 'react-dom/server'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { CaptainCandidate, CaptainRecommendation } from '../models/fpl'
import { fplQueryKeys } from '../queries/fplQueries'
import { TeamProvider } from '../team/TeamContext'
import { RecommendationsPage } from './RecommendationsPage'

describe('RecommendationsPage', () => {
  it('renders captain, vice captain, alternatives, and factor explanations', () => {
    const queryClient = new QueryClient()
    const recommendation: CaptainRecommendation = {
      teamId: 42,
      gameweek: 8,
      calculatedAt: '2026-10-20T12:00:00Z',
      bestCaptain: createCandidate(1, 'Mohamed Salah', 9.42, 8.75),
      viceCaptain: createCandidate(2, 'Cole Palmer', 8.71, 8.12),
      alternatives: [createCandidate(3, 'Bukayo Saka', 7.93, 7.55)],
    }

    queryClient.setQueryData(fplQueryKeys.captainRecommendation(42), recommendation)

    const markup = renderToStaticMarkup(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <TeamProvider initialTeamId={42}>
            <RecommendationsPage />
          </TeamProvider>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(markup).toContain('Gameweek 8')
    expect(markup).toContain('Captain')
    expect(markup).toContain('Mohamed Salah')
    expect(markup).toContain('9.42')
    expect(markup).toContain('Vice captain')
    expect(markup).toContain('Cole Palmer')
    expect(markup).toContain('Bukayo Saka')
    expect(markup).toContain('Projected points')
    expect(markup).toContain('Strong projection for the next fixture.')
  })
})

function createCandidate(playerId: number, playerName: string, projectedPoints: number, rankingScore: number): CaptainCandidate {
  return {
    playerId,
    playerName,
    teamName: 'Test FC',
    position: 'MID',
    projectedPoints,
    rankingScore,
    factors: [{
      factor: 'Projected points',
      score: projectedPoints * 0.65,
      explanation: 'Strong projection for the next fixture.',
    }],
  }
}
