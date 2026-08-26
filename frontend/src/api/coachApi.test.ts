import { afterEach, describe, expect, it, vi } from 'vitest'
import { sendCoachMessage } from './coachApi'

describe('sendCoachMessage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('posts the connected team ID and natural-language message', async () => {
    const response = {
      message: 'Mock coach reply.',
      teamId: 7558250,
      respondedAt: '2026-08-26T12:00:00Z',
      isMocked: true,
    }
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(sendCoachMessage({ teamId: 7558250, message: 'Should I sell Saka?' })).resolves.toEqual(response)
    expect(fetchMock).toHaveBeenCalledOnce()
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/coach/chat')
    expect(init.method).toBe('POST')
    expect(init.headers).toMatchObject({ Accept: 'application/json', 'Content-Type': 'application/json' })
    expect(JSON.parse(String(init.body))).toEqual({ teamId: 7558250, message: 'Should I sell Saka?' })
  })
})
