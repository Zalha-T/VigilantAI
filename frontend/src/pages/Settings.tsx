import { useState, useEffect } from 'react'
import { settingsApi, SystemSettings } from '../services/api'
import LoadingSpinner from '../components/LoadingSpinner'
import './Settings.css'

const Settings = () => {
  const [settings, setSettings] = useState<SystemSettings | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [thresholds, setThresholds] = useState({
    allowThreshold: 0.3,
    reviewThreshold: 0.5,
    blockThreshold: 0.7,
  })
  const [retrainThreshold, setRetrainThreshold] = useState(10)

  useEffect(() => {
    loadSettings()
  }, [])

  const loadSettings = async () => {
    const startTime = Date.now()
    try {
      setLoading(true)
      const data = await settingsApi.get()
      setSettings(data)
      setThresholds({
        allowThreshold: data.allowThreshold,
        reviewThreshold: data.reviewThreshold,
        blockThreshold: data.blockThreshold,
      })
      setRetrainThreshold(data.retrainThreshold)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error loading settings')
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

  const handleSaveThresholds = async () => {
    try {
      setSaving(true)
      setError(null)
      setSuccess(null)

      await settingsApi.updateThresholds(thresholds)
      setSuccess('Thresholds updated successfully!')
      
      // Reload settings to get updated values
      await loadSettings()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error updating thresholds')
    } finally {
      setSaving(false)
    }
  }

  const handleSaveRetrainThreshold = async () => {
    try {
      setSaving(true)
      setError(null)
      setSuccess(null)

      await settingsApi.updateRetrainThreshold({ retrainThreshold })
      setSuccess('Retrain threshold updated successfully!')
      
      // Reload settings to get updated values
      await loadSettings()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error updating retrain threshold')
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <LoadingSpinner />
  }

  return (
    <div className="settings">
      <h1>System Settings</h1>

      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      {success && (
        <div className="success-message">
          {success}
        </div>
      )}

      <div className="settings-section">
        <h2>Moderation Thresholds</h2>
        <p className="section-description">
          These thresholds determine how the agent classifies content:
        </p>
        <ul className="threshold-info">
          <li><strong>Allow Threshold:</strong> Content with final score below this value is automatically approved</li>
          <li><strong>Review Threshold:</strong> Content between Allow and Review thresholds goes to review queue</li>
          <li><strong>Block Threshold:</strong> Content with final score above this value is automatically blocked</li>
        </ul>

        <div className="threshold-inputs">
          <div className="input-group">
            <label htmlFor="allowThreshold">Allow Threshold</label>
            <input
              id="allowThreshold"
              type="number"
              min="0"
              max="1"
              step="0.01"
              value={thresholds.allowThreshold}
              onChange={(e) => setThresholds({ ...thresholds, allowThreshold: parseFloat(e.target.value) })}
              className="form-input"
            />
          </div>

          <div className="input-group">
            <label htmlFor="reviewThreshold">Review Threshold</label>
            <input
              id="reviewThreshold"
              type="number"
              min="0"
              max="1"
              step="0.01"
              value={thresholds.reviewThreshold}
              onChange={(e) => setThresholds({ ...thresholds, reviewThreshold: parseFloat(e.target.value) })}
              className="form-input"
            />
          </div>

          <div className="input-group">
            <label htmlFor="blockThreshold">Block Threshold</label>
            <input
              id="blockThreshold"
              type="number"
              min="0"
              max="1"
              step="0.01"
              value={thresholds.blockThreshold}
              onChange={(e) => setThresholds({ ...thresholds, blockThreshold: parseFloat(e.target.value) })}
              className="form-input"
            />
          </div>
        </div>

        <button
          onClick={handleSaveThresholds}
          disabled={saving}
          className="save-btn"
        >
          {saving ? 'Saving...' : 'Save Thresholds'}
        </button>
      </div>

      <div className="settings-section">
        <h2>Retraining Settings</h2>
        <p className="section-description">
          The agent automatically retrains when it accumulates enough gold labels (moderator feedback).
        </p>

        <div className="input-group">
          <label htmlFor="retrainThreshold">Retrain Threshold</label>
          <input
            id="retrainThreshold"
            type="number"
            min="1"
            max="1000"
            value={retrainThreshold}
            onChange={(e) => setRetrainThreshold(parseInt(e.target.value))}
            className="form-input"
          />
          <small>Number of new gold labels needed before retraining (current: {settings?.newGoldSinceLastTrain || 0} / {retrainThreshold})</small>
        </div>

        <button
          onClick={handleSaveRetrainThreshold}
          disabled={saving}
          className="save-btn"
        >
          {saving ? 'Saving...' : 'Save Retrain Threshold'}
        </button>
      </div>

      <div className="settings-section">
        <h2>Current Status</h2>
        <div className="status-info">
          <div className="status-item">
            <span className="status-label">New Gold Labels Since Last Train:</span>
            <span className="status-value">{settings?.newGoldSinceLastTrain || 0}</span>
          </div>
          <div className="status-item">
            <span className="status-label">Last Retrain Date:</span>
            <span className="status-value">
              {settings?.lastRetrainDate 
                ? new Date(settings.lastRetrainDate).toLocaleString()
                : 'Never'}
            </span>
          </div>
          <div className="status-item">
            <span className="status-label">Retraining Enabled:</span>
            <span className="status-value">{settings?.retrainingEnabled ? 'Yes' : 'No'}</span>
          </div>
        </div>
        {settings && settings.newGoldSinceLastTrain >= retrainThreshold && (
          <div className="warning-message" style={{ marginTop: '1rem', padding: '1rem', backgroundColor: '#ff9800', color: 'white', borderRadius: '4px' }}>
            ⚠️ Retraining threshold reached ({settings.newGoldSinceLastTrain} / {retrainThreshold}). 
            Retraining should start automatically within 5 minutes. 
            If it doesn't, check backend logs - you may need at least 10 total gold labels in the database.
          </div>
        )}
      </div>
    </div>
  )
}

export default Settings
