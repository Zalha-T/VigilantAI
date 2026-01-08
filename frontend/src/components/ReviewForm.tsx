import { contentApi } from '../services/api'

export const submitReview = async (
  contentId: string,
  goldLabel: number,
  correctDecision: boolean,
  feedback?: string
) => {
  return await contentApi.submitReview(contentId, {
    goldLabel,
    correctDecision,
    feedback: feedback || null,
    moderatorId: null
  })
}

export default { submitReview }
