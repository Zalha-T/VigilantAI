import './SkeletonCard.css'

const SkeletonCard = () => {
  return (
    <div className="skeleton-card">
      <div className="skeleton-header">
        <div className="skeleton-badge"></div>
        <div className="skeleton-text-small"></div>
      </div>
      <div className="skeleton-line"></div>
      <div className="skeleton-line"></div>
      <div className="skeleton-line-short"></div>
      <div className="skeleton-meta">
        <div className="skeleton-text-tiny"></div>
        <div className="skeleton-text-tiny"></div>
      </div>
    </div>
  )
}

export default SkeletonCard
