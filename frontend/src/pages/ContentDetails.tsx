import { useState, useEffect } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { contentApi, Content } from '../services/api'
import LoadingSpinner from '../components/LoadingSpinner'
import './ContentDetails.css'

const ContentDetails = () => {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [content, setContent] = useState<Content | null>(null)
  const [loading, setLoading] = useState(true)
  const [deleting, setDeleting] = useState(false)
  const [sendingToReview, setSendingToReview] = useState(false)

  useEffect(() => {
    if (id) {
      loadContent()
    }
  }, [id])

  const loadContent = async () => {
    if (!id) return
    setLoading(true)
    const startTime = Date.now()
    try {
      const data = await contentApi.getById(id)
      setContent(data)
    } catch (error) {
      console.error('Error loading content:', error)
    } finally {
      // Ensure loading spinner is visible for at least 500ms
      const elapsed = Date.now() - startTime
      const minDisplayTime = 500
      if (elapsed < minDisplayTime) {
        await new Promise(resolve => setTimeout(resolve, minDisplayTime - elapsed))
      }
      setLoading(false)
    }
  }

  const getStatusLabel = (status: number): string => {
    const labels: { [key: number]: string } = {
      1: 'Queued',
      2: 'Processing',
      3: 'Approved',
      4: 'Pending Review',
      5: 'Blocked'
    }
    return labels[status] || 'Unknown'
  }

  const getStatusClass = (status: number): string => {
    const classes: { [key: number]: string } = {
      1: 'status-queued',
      2: 'status-processing',
      3: 'status-approved',
      4: 'status-pending',
      5: 'status-blocked'
    }
    return classes[status] || ''
  }

  const handleDelete = async () => {
    if (!id) return
    
    if (!window.confirm('Are you sure you want to delete this content? This action cannot be undone.')) {
      return
    }

    setDeleting(true)
    try {
      await contentApi.delete(id)
      navigate('/')
    } catch (error) {
      console.error('Error deleting content:', error)
      alert('Error deleting content. Please try again.')
      setDeleting(false)
    }
  }

  const handleSendToReview = async () => {
    if (!id) return

    if (!window.confirm('Send this content to Review Queue? It will be available for moderator review.')) {
      return
    }

    setSendingToReview(true)
    try {
      await contentApi.sendToReview(id)
      // Reload content to show updated status
      await loadContent()
      alert('Content sent to Review Queue successfully!')
    } catch (error) {
      console.error('Error sending to review:', error)
      alert('Error sending content to review. Please try again.')
    } finally {
      setSendingToReview(false)
    }
  }

  if (loading) {
    return <LoadingSpinner />
  }

  if (!content) {
    return <div className="error">Content not found</div>
  }

  return (
    <div className="content-details">
      <Link to="/" className="back-link">← Back to Dashboard</Link>

      <div className="details-header">
        <h2>Content Details</h2>
        <div className="header-actions">
          <span className={`status-badge ${getStatusClass(content.status)}`}>
            {getStatusLabel(content.status)}
          </span>
          <div className="action-buttons">
            {content.status !== 4 && (
              <button
                onClick={handleSendToReview}
                disabled={sendingToReview}
                className="review-btn"
              >
                {sendingToReview ? 'Sending...' : 'Send to Review Queue'}
              </button>
            )}
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="delete-btn"
            >
              {deleting ? 'Deleting...' : 'Delete Content'}
            </button>
          </div>
        </div>
      </div>

      <div className="details-card">
        <div className="details-section">
          <h3>Content</h3>
          <p className="content-text">{content.text}</p>
          <div className="content-info">
            <span>Type: {content.type}</span>
            <span>Created: {new Date(content.createdAt).toLocaleString()}</span>
            {content.processedAt && (
              <span>Processed: {new Date(content.processedAt).toLocaleString()}</span>
            )}
          </div>
        </div>

        <div className="details-section">
          <h3>Author</h3>
          <div className="author-info">
            <span>Username: @{content.author.username}</span>
            <span>Reputation: {content.author.reputationScore}</span>
          </div>
        </div>

        {content.image && (
          <div className="details-section">
            <h3>Image</h3>
            <div className="image-section">
              <img 
                src={`https://localhost:60830${content.image.url}`} 
                alt={content.image.originalFileName}
                className="content-image"
              />
              {content.image.classification && (
                <div className="image-classification">
                  <div className={`classification-result ${content.image.classification.isBlocked ? 'blocked' : 'allowed'}`}>
                    <strong>Detected:</strong> {content.image.classification.label} 
                    ({(content.image.classification.confidence * 100).toFixed(1)}% confidence)
                    {content.image.classification.isBlocked ? (
                      <span className="blocked-badge"> - BLOCKED</span>
                    ) : (
                      <span className="allowed-badge"> - Allowed</span>
                    )}
                  </div>
                  {content.image.classification.details && (
                    <div className="classification-details">
                      {content.image.classification.details}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        <div className="details-section">
          <h3>Agent Prediction</h3>
          <div className="prediction-details">
            {content.prediction ? (
              <>
                <div className="prediction-item">
                  <strong>Decision:</strong> {content.prediction.decision === 1 ? 'Allow' : content.prediction.decision === 2 ? 'Review' : 'Block'}
                </div>
                <div className="prediction-item">
                  <strong>Final Score:</strong> {content.prediction.finalScore.toFixed(3)}
                </div>
              </>
            ) : (
              <div className="prediction-item">
                <strong>Status:</strong> <span style={{ color: '#888' }}>
                  {content.status === 1 ? 'Queued - waiting for agent processing' : 
                   content.status === 2 ? 'Processing - agent is analyzing content' : 
                   'No prediction available'}
                </span>
              </div>
            )}
            <div className="prediction-scores">
              <div className="score-item">
                <span>Spam:</span> <span className="score-value">
                  {content.prediction ? content.prediction.spamScore.toFixed(2) : '0.00'}
                </span>
              </div>
              <div className="score-item">
                <span>Toxic:</span> <span className="score-value">
                  {content.prediction ? content.prediction.toxicScore.toFixed(2) : '0.00'}
                </span>
              </div>
              <div className="score-item">
                <span>Hate:</span> <span className="score-value">
                  {content.prediction ? content.prediction.hateScore.toFixed(2) : '0.00'}
                </span>
              </div>
              <div className="score-item">
                <span>Offensive:</span> <span className="score-value">
                  {content.prediction ? content.prediction.offensiveScore.toFixed(2) : '0.00'}
                </span>
              </div>
            </div>
            {content.prediction && (
              <div className="prediction-labels">
                <h4>Labels:</h4>
                <div className="labels-list">
                  {content.labels.isSpam && (
                    <span className="label-badge label-spam">
                      Spam {content.prediction && `(${content.prediction.spamScore.toFixed(2)})`}
                    </span>
                  )}
                  {content.labels.isToxic && (
                    <span className="label-badge label-toxic">
                      Toxic {content.prediction && `(${content.prediction.toxicScore.toFixed(2)})`}
                    </span>
                  )}
                  {content.labels.isHate && (
                    <span className="label-badge label-hate">
                      Hate {content.prediction && `(${content.prediction.hateScore.toFixed(2)})`}
                    </span>
                  )}
                  {content.labels.isOffensive && (
                    <span className="label-badge label-offensive">
                      Offensive {content.prediction && `(${content.prediction.offensiveScore.toFixed(2)})`}
                    </span>
                  )}
                  {!content.labels.isSpam && !content.labels.isToxic && !content.labels.isHate && !content.labels.isOffensive && (
                    <span className="no-labels">No problematic labels</span>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>

        {content.review && (
          <div className="details-section">
            <h3>Review</h3>
            <div className="review-details">
              <div className="review-item">
                <strong>Gold Label:</strong> {content.review.goldLabel === 1 ? 'Allow' : content.review.goldLabel === 2 ? 'Review' : 'Block'}
              </div>
              {content.review.correctDecision !== null && (
                <div className="review-item">
                  <strong>Agent Correct:</strong> {content.review.correctDecision ? 'Yes' : 'No'}
                </div>
              )}
              {content.review.feedback && (
                <div className="review-item">
                  <strong>Feedback:</strong> {content.review.feedback}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default ContentDetails
