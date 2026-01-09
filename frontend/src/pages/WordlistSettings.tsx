import { useState, useEffect } from 'react'
import { wordlistApi, BlockedWord } from '../services/api'
import LoadingSpinner from '../components/LoadingSpinner'
import './Settings.css'

const WordlistSettings = () => {
  const [words, setWords] = useState<BlockedWord[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  
  const [newWord, setNewWord] = useState('')
  const [newCategory, setNewCategory] = useState('toxic')
  const [filterCategory, setFilterCategory] = useState<string>('all')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editWord, setEditWord] = useState('')
  const [editCategory, setEditCategory] = useState('')

  const categories = ['toxic', 'hate', 'spam', 'offensive', 'slur']

  useEffect(() => {
    loadWords()
  }, [])

  const loadWords = async () => {
    const startTime = Date.now()
    try {
      setLoading(true)
      const data = await wordlistApi.getAll()
      setWords(data)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error loading wordlist')
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

  const handleAddWord = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newWord.trim()) {
      setError('Word cannot be empty')
      return
    }

    try {
      setError(null)
      setSuccess(null)
      await wordlistApi.add({ word: newWord.trim(), category: newCategory })
      setSuccess(`Word "${newWord}" added successfully!`)
      setNewWord('')
      await loadWords()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error adding word')
    }
  }

  const handleDeleteWord = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this word?')) {
      return
    }

    try {
      setError(null)
      setSuccess(null)
      await wordlistApi.delete(id)
      setSuccess('Word deleted successfully!')
      await loadWords()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error deleting word')
    }
  }

  const handleToggleActive = async (word: BlockedWord) => {
    try {
      setError(null)
      setSuccess(null)
      await wordlistApi.update(word.id, { isActive: !word.isActive })
      setSuccess(`Word ${word.isActive ? 'deactivated' : 'activated'} successfully!`)
      await loadWords()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error updating word')
    }
  }

  const startEdit = (word: BlockedWord) => {
    setEditingId(word.id)
    setEditWord(word.word)
    setEditCategory(word.category)
  }

  const cancelEdit = () => {
    setEditingId(null)
    setEditWord('')
    setEditCategory('')
  }

  const handleSaveEdit = async (id: string) => {
    try {
      setError(null)
      setSuccess(null)
      await wordlistApi.update(id, { word: editWord.trim(), category: editCategory })
      setSuccess('Word updated successfully!')
      setEditingId(null)
      await loadWords()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error updating word')
    }
  }

  const filteredWords = filterCategory === 'all' 
    ? words 
    : words.filter(w => w.category === filterCategory)

  if (loading) {
    return <LoadingSpinner />
  }

  return (
    <div className="settings">
      <h1>Blocked Words List</h1>

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
        <h2>Add New Word</h2>
        <form onSubmit={handleAddWord} className="add-word-form">
          <div className="form-row">
            <div className="input-group">
              <label htmlFor="newWord">Word</label>
              <input
                id="newWord"
                type="text"
                value={newWord}
                onChange={(e) => setNewWord(e.target.value)}
                className="form-input"
                placeholder="Enter word to block"
                maxLength={100}
              />
            </div>
            <div className="input-group">
              <label htmlFor="newCategory">Category</label>
              <select
                id="newCategory"
                value={newCategory}
                onChange={(e) => setNewCategory(e.target.value)}
                className="form-input"
              >
                {categories.map(cat => (
                  <option key={cat} value={cat}>{cat}</option>
                ))}
              </select>
            </div>
            <button type="submit" className="add-btn">Add Word</button>
          </div>
        </form>
      </div>

      <div className="settings-section">
        <h2>Word List ({filteredWords.length} words)</h2>
        
        <div className="filter-controls">
          <label>Filter by category:</label>
          <select
            value={filterCategory}
            onChange={(e) => setFilterCategory(e.target.value)}
            className="filter-select"
          >
            <option value="all">All Categories</option>
            {categories.map(cat => (
              <option key={cat} value={cat}>{cat}</option>
            ))}
          </select>
        </div>

        <div className="wordlist-table">
          <table>
            <thead>
              <tr>
                <th>Word</th>
                <th>Category</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredWords.length === 0 ? (
                <tr>
                  <td colSpan={4} className="empty-message">No words found</td>
                </tr>
              ) : (
                filteredWords.map(word => (
                  <tr key={word.id} className={!word.isActive ? 'inactive' : ''}>
                    <td>
                      {editingId === word.id ? (
                        <input
                          type="text"
                          value={editWord}
                          onChange={(e) => setEditWord(e.target.value)}
                          className="form-input inline-input"
                        />
                      ) : (
                        word.word
                      )}
                    </td>
                    <td>
                      {editingId === word.id ? (
                        <select
                          value={editCategory}
                          onChange={(e) => setEditCategory(e.target.value)}
                          className="form-input inline-input"
                        >
                          {categories.map(cat => (
                            <option key={cat} value={cat}>{cat}</option>
                          ))}
                        </select>
                      ) : (
                        <span className={`category-badge category-${word.category}`}>
                          {word.category}
                        </span>
                      )}
                    </td>
                    <td>
                      <span className={`status-badge ${word.isActive ? 'active' : 'inactive'}`}>
                        {word.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td>
                      {editingId === word.id ? (
                        <div className="action-buttons">
                          <button
                            onClick={() => handleSaveEdit(word.id)}
                            className="save-btn-small"
                          >
                            Save
                          </button>
                          <button
                            onClick={cancelEdit}
                            className="cancel-btn-small"
                          >
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <div className="action-buttons">
                          <button
                            onClick={() => startEdit(word)}
                            className="edit-btn"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleToggleActive(word)}
                            className={word.isActive ? 'deactivate-btn' : 'activate-btn'}
                          >
                            {word.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                          <button
                            onClick={() => handleDeleteWord(word.id)}
                            className="delete-btn-small"
                          >
                            Delete
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

export default WordlistSettings
