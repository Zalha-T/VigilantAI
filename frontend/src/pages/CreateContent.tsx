import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { contentApi } from '../services/api'
import './CreateContent.css'

const CreateContent = () => {
  const navigate = useNavigate()
  const [formData, setFormData] = useState({
    type: 1, // Comment
    text: '',
    authorUsername: '',
    threadId: null as string | null
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    setSuccess(false)

    try {
      const response = await contentApi.create({
        type: formData.type,
        text: formData.text,
        authorUsername: formData.authorUsername || 'anonymous',
        threadId: formData.threadId || null
      })

      setSuccess(true)
      
      // Reset form
      setFormData({
        type: 1,
        text: '',
        authorUsername: '',
        threadId: null
      })

      // Redirect to dashboard after 2 seconds
      setTimeout(() => {
        navigate('/')
      }, 2000)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error creating content')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="create-content">
      <div className="create-header">
        <h2>Create New Content</h2>
        <button onClick={() => navigate('/')} className="back-btn">← Back to Dashboard</button>
      </div>

      <div className="content-types-info">
        <h3>Content Types:</h3>
        <div className="type-info-grid">
          <div className="type-info-card">
            <h4>Comment (1)</h4>
            <p>Komentar na post ili drugi komentar. Najčešći tip sadržaja.</p>
          </div>
          <div className="type-info-card">
            <h4>Post (2)</h4>
            <p>Glavni post ili objava. Obično duži i više strukturiran sadržaj.</p>
          </div>
          <div className="type-info-card">
            <h4>Message (3)</h4>
            <p>Privatna poruka između korisnika. Može biti direktnija komunikacija.</p>
          </div>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="create-form">
        <div className="form-group">
          <label htmlFor="type">Content Type *</label>
          <select
            id="type"
            value={formData.type}
            onChange={(e) => setFormData({ ...formData, type: parseInt(e.target.value) })}
            required
            className="form-input"
          >
            <option value={1}>Comment</option>
            <option value={2}>Post</option>
            <option value={3}>Message</option>
          </select>
        </div>

        <div className="form-group">
          <label htmlFor="text">Content Text *</label>
          <textarea
            id="text"
            value={formData.text}
            onChange={(e) => setFormData({ ...formData, text: e.target.value })}
            required
            rows={6}
            className="form-input"
            placeholder="Enter your content here..."
            maxLength={5000}
          />
          <span className="char-count">{formData.text.length} / 5000</span>
        </div>

        <div className="form-group">
          <label htmlFor="authorUsername">Author Username *</label>
          <input
            id="authorUsername"
            type="text"
            value={formData.authorUsername}
            onChange={(e) => setFormData({ ...formData, authorUsername: e.target.value })}
            required
            className="form-input"
            placeholder="Enter author username"
            maxLength={100}
          />
          <small>If user doesn't exist, it will be created automatically</small>
        </div>

        <div className="form-group">
          <label htmlFor="threadId">Thread ID (Optional)</label>
          <input
            id="threadId"
            type="text"
            value={formData.threadId || ''}
            onChange={(e) => setFormData({ ...formData, threadId: e.target.value || null })}
            className="form-input"
            placeholder="Enter thread ID if this is a reply"
          />
          <small>Leave empty if this is a new thread</small>
        </div>

        {error && (
          <div className="error-message">
            {error}
          </div>
        )}

        {success && (
          <div className="success-message">
            ✓ Content created successfully! Redirecting to dashboard...
          </div>
        )}

        <button
          type="submit"
          disabled={loading || !formData.text.trim() || !formData.authorUsername.trim()}
          className="submit-btn"
        >
          {loading ? 'Creating...' : 'Create Content'}
        </button>
      </form>

      <div className="info-box">
        <h3>ℹ️ What happens after creation?</h3>
        <ol>
          <li>Content is added to the queue (Status: Queued)</li>
          <li>Agent automatically processes it (2-3 seconds)</li>
          <li>Agent classifies it: <strong>Allow</strong>, <strong>Review</strong>, or <strong>Block</strong></li>
          <li>You can see the result in Dashboard</li>
          <li>If it's in Review, you can give feedback in Review Queue</li>
        </ol>
      </div>
    </div>
  )
}

export default CreateContent
