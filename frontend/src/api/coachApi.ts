import type { CoachChatRequest, CoachChatResponse } from '../models/coach'
import { request } from './fplApi'

export function sendCoachMessage(payload: CoachChatRequest, signal?: AbortSignal) {
  return request<CoachChatResponse>('/api/coach/chat', signal, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}