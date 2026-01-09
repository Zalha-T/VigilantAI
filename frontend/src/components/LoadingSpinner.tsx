import './LoadingSpinner.css'

const LoadingSpinner = () => {
  return (
    <div className="loading-overlay">
      <div className="loading-container">
        <div className="loading-circle">
          <div className="loading-ring loading-ring-outer"></div>
          <div className="loading-ring loading-ring-inner"></div>
        </div>
        <img 
          src="/logo.png" 
          alt="VigilantAI Logo" 
          className="loading-logo"
          onError={(e) => {
            console.error('Logo failed to load:', e);
            (e.target as HTMLImageElement).style.display = 'none';
          }}
        />
      </div>
    </div>
  )
}

export default LoadingSpinner
