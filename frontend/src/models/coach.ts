export interface CoachChatRequest {
  teamId: number
  message: string
}

export interface CoachChatResponse {
  message: string
  teamId: number
  respondedAt: string
  isMocked: boolean
  recommendationType: CoachRecommendationType
  confidence: number
  player: CoachPlayerInfo | null
}

export type CoachRecommendationType = 'General' | 'Availability' | 'Transfer' | 'Replacement'

export interface CoachPlayerInfo {
  playerId: number
  playerName: string
  teamName: string
  position: string
  status: string
  chanceOfPlayingNextRound: number | null
  photoUrl: string
}

export interface CoachChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  sentAt: string
  isMocked?: boolean
  recommendationType?: CoachRecommendationType
  confidence?: number
  player?: CoachPlayerInfo | null
}