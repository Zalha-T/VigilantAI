import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:60830/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export interface Content {
  id: string
  text: string
  type: number
  status: number
  createdAt: string
  processedAt: string | null
  author: {
    username: string
    reputationScore: number
  }
  prediction: {
    decision: number
    finalScore: number
    spamScore: number
    toxicScore: number
    hateScore: number
    offensiveScore: number
    confidence: number
  } | null
  review: {
    goldLabel: number | null
    correctDecision: boolean | null
    feedback: string | null
  } | null
  labels: {
    isSpam: boolean
    isToxic: boolean
    isHate: boolean
    isOffensive: boolean
    isProblematic: boolean
    agentDecision: number | null
    humanLabel: number | null
  }
  image?: ContentImage | null
}

// Simplified content for pending review (from API)
export interface PendingReviewContent {
  id: string
  text: string
  type: number
  status: number
  createdAt: string
  author: {
    username: string
    reputationScore: number
  }
  prediction: {
    decision: number
    finalScore: number
    spamScore: number
    toxicScore: number
    hateScore: number
    offensiveScore: number
    confidence: number
  } | null
}

export interface ContentListResponse {
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  data: Content[]
}

export interface CreateContentRequest {
  type: number
  text: string
  authorUsername: string
  threadId?: string | null
  image?: File | null
}

export interface ContentImage {
  id: string
  fileName: string
  originalFileName: string
  url: string
  classification: {
    label: string
    confidence: number
    isBlocked: boolean
    details: string
    topPredictions?: Array<{
      label: string
      confidence: number
      classIndex: number
    }>
  } | null
}

export interface SubmitReviewRequest {
  goldLabel: number
  correctDecision: boolean | null
  feedback?: string | null
  moderatorId?: string | null
}

export const contentApi = {
  // Get all content with pagination
  getAll: async (status?: number, search?: string, page: number = 1, pageSize: number = 50): Promise<ContentListResponse> => {
    const params: any = { page, pageSize }
    if (status !== undefined) params.status = status
    if (search && search.trim()) params.search = search.trim()
    const response = await api.get<ContentListResponse>('/content', { params })
    return response.data
  },

  // Get single content by ID
  getById: async (id: string): Promise<Content> => {
    const response = await api.get<Content>(`/content/${id}`)
    return response.data
  },

  // Get pending review content
  getPendingReview: async (): Promise<PendingReviewContent[]> => {
    const response = await api.get<PendingReviewContent[]>('/content/pending-review')
    return response.data
  },

  // Create new content
  create: async (data: CreateContentRequest): Promise<{ contentId: string; status: string; imageId?: string; imageClassification?: any }> => {
    // If image is provided, use FormData
    if (data.image) {
      const formData = new FormData()
      formData.append('type', data.type.toString())
      formData.append('text', data.text)
      formData.append('authorUsername', data.authorUsername)
      if (data.threadId) {
        formData.append('threadId', data.threadId)
      }
      formData.append('image', data.image)
      
      const response = await api.post('/content', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      })
      return response.data
    } else {
      // No image, use JSON
      const response = await api.post('/content', {
        type: data.type,
        text: data.text,
        authorUsername: data.authorUsername,
        threadId: data.threadId || null
      })
      return response.data
    }
  },

  // Submit review (gold label)
  submitReview: async (contentId: string, data: SubmitReviewRequest): Promise<{ reviewId: string }> => {
    const response = await api.post(`/review/${contentId}/review`, data)
    return response.data
  },

  // Reset stuck content
  resetStuck: async (): Promise<{ resetCount: number; message: string }> => {
    const response = await api.post('/content/reset-stuck')
    return response.data
  },

  // Delete content
  delete: async (id: string): Promise<{ message: string }> => {
    const response = await api.delete(`/content/${id}`)
    return response.data
  },

  // Send content to review queue
  sendToReview: async (id: string): Promise<{ message: string; status: number }> => {
    const response = await api.post(`/content/${id}/send-to-review`)
    return response.data
  },
}

export interface SystemSettings {
  allowThreshold: number
  reviewThreshold: number
  blockThreshold: number
  retrainThreshold: number
  newGoldSinceLastTrain: number
  lastRetrainDate: string | null
  retrainingEnabled: boolean
}

export interface UpdateThresholdsRequest {
  allowThreshold: number
  reviewThreshold: number
  blockThreshold: number
}

export interface UpdateRetrainThresholdRequest {
  retrainThreshold: number
}

export const settingsApi = {
  // Get current settings
  get: async (): Promise<SystemSettings> => {
    const response = await api.get<SystemSettings>('/settings')
    return response.data
  },

  // Update thresholds
  updateThresholds: async (data: UpdateThresholdsRequest): Promise<{ message: string }> => {
    const response = await api.put('/settings/thresholds', data)
    return response.data
  },

  // Update retrain threshold
  updateRetrainThreshold: async (data: UpdateRetrainThresholdRequest): Promise<{ message: string }> => {
    const response = await api.post('/settings/retrain-threshold', data)
    return response.data
  },
}

export interface BlockedWord {
  id: string
  word: string
  category: string
  createdAt: string
  updatedAt: string | null
  isActive: boolean
}

export interface AddWordRequest {
  word: string
  category: string
}

export interface UpdateWordRequest {
  word?: string
  category?: string
  isActive?: boolean
}

export const wordlistApi = {
  // Get all blocked words
  getAll: async (): Promise<BlockedWord[]> => {
    const response = await api.get<BlockedWord[]>('/wordlist')
    return response.data
  },

  // Get words by category
  getByCategory: async (category: string): Promise<BlockedWord[]> => {
    const response = await api.get<BlockedWord[]>(`/wordlist/category/${category}`)
    return response.data
  },

  // Add word
  add: async (data: AddWordRequest): Promise<BlockedWord> => {
    const response = await api.post<BlockedWord>('/wordlist', data)
    return response.data
  },

  // Update word
  update: async (id: string, data: UpdateWordRequest): Promise<BlockedWord> => {
    const response = await api.put<BlockedWord>(`/wordlist/${id}`, data)
    return response.data
  },

  // Delete word
  delete: async (id: string): Promise<{ message: string }> => {
    const response = await api.delete(`/wordlist/${id}`)
    return response.data
  },
}

export default api
