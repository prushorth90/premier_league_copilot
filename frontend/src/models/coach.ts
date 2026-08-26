export interface CoachChatRequest {
  teamId: number
  message: string
}

export interface CoachChatResponse {
  message: string
  teamId: number
  respondedAt: string
  isMocked: boolean
}

export interface CoachChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  sentAt: string
  isMocked?: boolean
}