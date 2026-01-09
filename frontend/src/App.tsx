import { BrowserRouter, Routes, Route, Link } from 'react-router-dom'
import Dashboard from './pages/Dashboard'
import ReviewQueue from './pages/ReviewQueue'
import ContentDetails from './pages/ContentDetails'
import CreateContent from './pages/CreateContent'
import Settings from './pages/Settings'
import WordlistSettings from './pages/WordlistSettings'
import './App.css'

function App() {
  return (
    <BrowserRouter>
      <div className="app">
        <nav className="navbar">
          <div className="nav-container">
            <div className="nav-brand">
              <img src="/logo.png" alt="VigilantAI Logo" className="nav-logo" />
              <h1 className="nav-title">VigilantAI</h1>
            </div>
            <div className="nav-links">
              <Link to="/" className="nav-link">Dashboard</Link>
              <Link to="/review" className="nav-link">Review Queue</Link>
              <Link to="/create" className="nav-link">Create Content</Link>
              <Link to="/settings" className="nav-link">Settings</Link>
              <Link to="/wordlist" className="nav-link">Wordlist</Link>
            </div>
          </div>
        </nav>

        <main className="main-content">
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/review" element={<ReviewQueue />} />
            <Route path="/create" element={<CreateContent />} />
            <Route path="/settings" element={<Settings />} />
            <Route path="/wordlist" element={<WordlistSettings />} />
            <Route path="/content/:id" element={<ContentDetails />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  )
}

export default App
