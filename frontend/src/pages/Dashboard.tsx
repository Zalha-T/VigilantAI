import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { contentApi, Content } from '../services/api'
import { onModerationResult } from '../services/signalr'
import './Dashboard.css'

const Dashboard = () => {
  const [contents, setContents] = useState<Content[]>([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState<number | undefined>(undefined)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)

  const loadContents = async () => {
    setLoading(true)
    try {
      const response = await contentApi.getAll(statusFilter, page, 50)
      setContents(response.data)
      setTotalPages(response.totalPages)
    } catch (error) {
      console.error('Error loading contents:', error)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadContents()

    // Setup SignalR for real-time updates
    onModerationResult(() => {
      // Refresh content when new moderation result arrives
      loadContents()
    })
  }, [statusFilter, page])

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

  const getDecisionLabel = (decision: number | null): string => {
    if (decision === null) return 'N/A'
    const labels: { [key: number]: string } = {
      1: 'Allow',
      2: 'Review',
      3: 'Block'
    }
    return labels[decision] || 'Unknown'
  }

  const handleDelete = async (e: React.MouseEvent, contentId: string) => {
    e.preventDefault()
    e.stopPropagation()
    
    if (!window.confirm('Are you sure you want to delete this content? This action cannot be undone.')) {
      return
    }

    try {
      await contentApi.delete(contentId)
      // Remove from list immediately
      setContents(prev => prev.filter(c => c.id !== contentId))
    } catch (error) {
      console.error('Error deleting content:', error)
      alert('Error deleting content. Please try again.')
    }
  }

  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <h2>Content Dashboard</h2>
        <div className="dashboard-controls">
          <select
            value={statusFilter || ''}
            onChange={(e) => setStatusFilter(e.target.value ? parseInt(e.target.value) : undefined)}
            className="filter-select"
          >
            <option value="">All Statuses</option>
            <option value="1">Queued</option>
            <option value="2">Processing</option>
            <option value="3">Approved</option>
            <option value="4">Pending Review</option>
            <option value="5">Blocked</option>
          </select>
          <button onClick={loadContents} className="refresh-btn">Refresh</button>
        </div>
      </div>

      {loading ? (
        <div className="loading">Loading...</div>
      ) : (
        <>
          <div className="content-grid">
            {contents.map((content) => (
              <div key={content.id} className="content-card-wrapper">
                <Link
                  to={`/content/${content.id}`}
                  className="content-card"
                >
                  <div className="content-header">
                    <span className={`status-badge ${getStatusClass(content.status)}`}>
                      {getStatusLabel(content.status)}
                    </span>
                    <span className="content-type">Type: {content.type}</span>
                  </div>
                <p className="content-text">{content.text}</p>
                <div className="content-meta">
                  <span className="author">@{content.author.username}</span>
                  <span className="reputation">Rep: {content.author.reputationScore}</span>
                </div>
                {content.prediction && (
                  <div className="content-prediction">
                    <div className="prediction-decision">
                      Decision: <strong>{getDecisionLabel(content.prediction.decision)}</strong>
                    </div>
                    <div className="prediction-score">
                      Score: {content.prediction.finalScore.toFixed(2)}
                    </div>
                    <div className="prediction-labels">
                      {content.labels.isSpam && <span className="label-badge label-spam">Spam</span>}
                      {content.labels.isToxic && <span className="label-badge label-toxic">Toxic</span>}
                      {content.labels.isHate && <span className="label-badge label-hate">Hate</span>}
                      {content.labels.isOffensive && <span className="label-badge label-offensive">Offensive</span>}
                    </div>
                  </div>
                )}
                {!content.prediction && (
                  <div className="no-prediction">No prediction yet</div>
                )}
                </Link>
                <button
                  onClick={(e) => handleDelete(e, content.id)}
                  className="delete-btn"
                  title="Delete content"
                >
                  🗑️
                </button>
              </div>
            ))}
          </div>

          {contents.length === 0 && (
            <div className="empty-state">No content found</div>
          )}

          {totalPages > 1 && (
            <div className="pagination">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                className="page-btn"
              >
                Previous
              </button>
              <span className="page-info">Page {page} of {totalPages}</span>
              <button
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="page-btn"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}

export default Dashboard
