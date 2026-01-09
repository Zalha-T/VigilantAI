import { useState, useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { contentApi, PendingReviewContent } from '../services/api'
import LoadingSpinner from '../components/LoadingSpinner'
import { showToast } from '../components/ToastContainer'
import './ReviewQueue.css'

const ReviewQueue = () => {
  const [contents, setContents] = useState<PendingReviewContent[]>([])
  const [loading, setLoading] = useState(true)
  const [processing, setProcessing] = useState<Set<string>>(new Set())
  const scrollPositionRef = useRef<number>(0)
  const containerRef = useRef<HTMLDivElement>(null)

  const loadPendingReview = async (preserveScroll = false) => {
    if (preserveScroll && containerRef.current) {
      scrollPositionRef.current = containerRef.current.scrollTop
    }

    const startTime = Date.now()
    try {
      const data = await contentApi.getPendingReview()
      setContents(data)
    } catch (error) {
      console.error('Error loading pending review:', error)
    } finally {
      // Ensure loading spinner is visible for at least 500ms
      const elapsed = Date.now() - startTime
      const minDisplayTime = 500
      if (elapsed < minDisplayTime) {
        await new Promise(resolve => setTimeout(resolve, minDisplayTime - elapsed))
      }
      setLoading(false)
      
      // Restore scroll position after a brief delay
      if (preserveScroll && containerRef.current) {
        setTimeout(() => {
          if (containerRef.current) {
            containerRef.current.scrollTop = scrollPositionRef.current
          }
        }, 100)
      }
    }
  }

  useEffect(() => {
    loadPendingReview()
    // Refresh every 10 seconds (less frequent)
    const interval = setInterval(() => loadPendingReview(true), 10000)
    return () => clearInterval(interval)
  }, [])

  const handleReview = async (contentId: string, goldLabel: number, correctDecision: boolean, feedback?: string) => {
    // Prevent multiple clicks
    if (processing.has(contentId)) {
      return
    }

    setProcessing(prev => new Set(prev).add(contentId))

    try {
      await contentApi.submitReview(contentId, {
        goldLabel,
        correctDecision,
        feedback: feedback || null,
        moderatorId: null
      })
      
      // Remove from list immediately (optimistic update)
      setContents(prev => prev.filter(c => c.id !== contentId))
      
      const decisionLabel = goldLabel === 1 ? 'Allow' : goldLabel === 2 ? 'Review' : 'Block'
      showToast(`Review submitted: ${decisionLabel}`, 'success')
      
      // Refresh list to get updated data
      await loadPendingReview(true)
    } catch (error) {
      console.error('Error submitting review:', error)
      const errorMsg = error instanceof Error ? error.message : 'Unknown error'
      showToast(`Error submitting review: ${errorMsg}`, 'error')
      // Reload on error to restore state
      await loadPendingReview(true)
    } finally {
      setProcessing(prev => {
        const newSet = new Set(prev)
        newSet.delete(contentId)
        return newSet
      })
    }
  }

  const getDecisionLabel = (decision: number | null): string => {
    if (decision === null) return 'N/A'
    const labels: { [key: number]: string } = {
      1: 'Allow',
      2: 'Review',
      3: 'Block'
    }
    return labels[decision] || 'Unknown'
  }

  return (
    <div className="review-queue">
      <div className="review-header">
        <h2>Review Queue</h2>
        <div className="header-actions">
          <span className="queue-count">{contents.length} pending</span>
          <button onClick={() => loadPendingReview(false)} className="refresh-btn" disabled={loading}>
            {loading ? 'Loading...' : 'Refresh'}
          </button>
        </div>
      </div>

      {loading && contents.length === 0 ? (
        <LoadingSpinner />
      ) : (
        <>
          {contents.length === 0 ? (
            <div className="empty-state">No content pending review</div>
          ) : (
            <div className="review-list" ref={containerRef}>
              {contents.map((content) => {
                const isProcessing = processing.has(content.id)
                return (
                  <div key={content.id} className={`review-card ${isProcessing ? 'processing' : ''}`}>
                    <div className="review-content">
                      <div className="review-header-content">
                        <Link to={`/content/${content.id}`} className="review-link">
                          <h3>Content #{content.id.substring(0, 8)}</h3>
                        </Link>
                        {content.prediction && (
                          <span className="agent-decision-badge">
                            Agent: {getDecisionLabel(content.prediction.decision)}
                          </span>
                        )}
                      </div>
                      <p className="review-text">{content.text}</p>
                      <div className="review-meta">
                        <div className="meta-row">
                          <span className="meta-label">Author:</span>
                          <span className="author-name">@{content.author.username}</span>
                          <span className="meta-separator">•</span>
                          <span className="reputation">Rep: {content.author.reputationScore}</span>
                        </div>
                        {content.prediction && (
                          <div className="meta-row">
                            <span className="meta-label">Score:</span>
                            <span className="score-value">{content.prediction.finalScore.toFixed(3)}</span>
                            <span className="meta-separator">•</span>
                            <span className="meta-label">Confidence:</span>
                            <span className="confidence">
                              {content.prediction.confidence === 1 ? 'Low' : content.prediction.confidence === 2 ? 'Medium' : 'High'}
                            </span>
                          </div>
                        )}
                        <div className="meta-row">
                          <span className="meta-label">Created:</span>
                          <span>{new Date(content.createdAt).toLocaleString()}</span>
                        </div>
                      </div>
                      {content.prediction && (
                        <div className="prediction-scores-mini">
                          <span className={content.prediction.spamScore > 0.5 ? 'score-high' : 'score-low'}>
                            Spam: {content.prediction.spamScore.toFixed(2)}
                          </span>
                          <span className={content.prediction.toxicScore > 0.5 ? 'score-high' : 'score-low'}>
                            Toxic: {content.prediction.toxicScore.toFixed(2)}
                          </span>
                          <span className={content.prediction.hateScore > 0.5 ? 'score-high' : 'score-low'}>
                            Hate: {content.prediction.hateScore.toFixed(2)}
                          </span>
                          <span className={content.prediction.offensiveScore > 0.5 ? 'score-high' : 'score-low'}>
                            Offensive: {content.prediction.offensiveScore.toFixed(2)}
                          </span>
                        </div>
                      )}
                    </div>
                    <div className="review-actions">
                      <button
                        onClick={() => handleReview(content.id, 1, content.prediction?.decision === 1, 'Approved by moderator')}
                        className="action-btn action-allow"
                        disabled={isProcessing}
                      >
                        {isProcessing ? '...' : '✓ Allow'}
                      </button>
                      <button
                        onClick={() => handleReview(content.id, 2, content.prediction?.decision === 2, 'Needs more review')}
                        className="action-btn action-review"
                        disabled={isProcessing}
                      >
                        {isProcessing ? '...' : '⚠ Review'}
                      </button>
                      <button
                        onClick={() => handleReview(content.id, 3, content.prediction?.decision === 3, 'Blocked by moderator')}
                        className="action-btn action-block"
                        disabled={isProcessing}
                      >
                        {isProcessing ? '...' : '✗ Block'}
                      </button>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </>
      )}
    </div>
  )
}

export default ReviewQueue
