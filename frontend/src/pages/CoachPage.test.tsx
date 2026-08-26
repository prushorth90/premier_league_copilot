// @vitest-environment jsdom

import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/fplApi'

const coachApiMock = vi.hoisted(() => ({ sendCoachMessage: vi.fn() }))

vi.mock('../api/coachApi', () => ({ sendCoachMessage: coachApiMock.sendCoachMessage }))
vi.mock('../team/useTeam', () => ({ useTeam: () => ({ teamId: 7558250 }) }))

import { CoachPage } from './CoachPage'

describe('CoachPage', () => {
  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
  })

  it('sends the connected Team ID and displays the mocked assistant response', async () => {
    coachApiMock.sendCoachMessage.mockResolvedValue({
      message: 'Compare Saka with the best same-position replacements.',
      teamId: 7558250,
      respondedAt: '2026-08-26T12:00:00Z',
      isMocked: true,
    })
    const user = userEvent.setup()
    render(<CoachPage />)

    await user.type(screen.getByLabelText('Message AI Coach'), 'Should I sell Saka?')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(await screen.findByText('Compare Saka with the best same-position replacements.')).toBeTruthy()
    expect(coachApiMock.sendCoachMessage).toHaveBeenCalledWith({ teamId: 7558250, message: 'Should I sell Saka?' })
    expect(screen.getAllByText('Mocked response').length).toBeGreaterThanOrEqual(2)
  })

  it('shows a pending assistant state while waiting', async () => {
    let resolveResponse: (value: unknown) => void = () => undefined
    coachApiMock.sendCoachMessage.mockReturnValue(new Promise((resolve) => { resolveResponse = resolve }))
    const user = userEvent.setup()
    render(<CoachPage />)

    await user.type(screen.getByLabelText('Message AI Coach'), 'Saka is injured')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(screen.getByText('Coach is thinking')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Send message' }) as HTMLButtonElement).disabled).toBe(true)

    resolveResponse({ message: 'Noted.', teamId: 7558250, respondedAt: '2026-08-26T12:00:00Z', isMocked: true })
    expect(await screen.findByText('Noted.')).toBeTruthy()
  })

  it('shows an error and retries without duplicating the user message', async () => {
    coachApiMock.sendCoachMessage
      .mockRejectedValueOnce(new ApiError('Mock coach outage.', 503))
      .mockResolvedValueOnce({ message: 'Recovered reply.', teamId: 7558250, respondedAt: '2026-08-26T12:00:00Z', isMocked: true })
    const user = userEvent.setup()
    render(<CoachPage />)

    await user.type(screen.getByLabelText('Message AI Coach'), 'Sell Martinelli?')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect((await screen.findByRole('alert')).textContent).toContain('Mock coach outage.')
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('Recovered reply.')).toBeTruthy()
    expect(coachApiMock.sendCoachMessage).toHaveBeenCalledTimes(2)
    expect(screen.getAllByText('Sell Martinelli?')).toHaveLength(1)
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull())
  })
})
