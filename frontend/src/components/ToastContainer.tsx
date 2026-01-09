import { useEffect, useState } from 'react'
import Toast, { ToastProps } from './Toast'

export interface ToastMessage {
  id: string
  message: string
  type: 'success' | 'error' | 'info' | 'warning'
  duration?: number
}

let toastId = 0
const toastListeners: Array<(toast: ToastMessage) => void> = []

export const showToast = (message: string, type: ToastMessage['type'] = 'info', duration?: number) => {
  const id = `toast-${++toastId}`
  const toast: ToastMessage = { id, message, type, duration }
  toastListeners.forEach(listener => listener(toast))
}

const ToastContainer = () => {
  const [toasts, setToasts] = useState<ToastMessage[]>([])

  useEffect(() => {
    const listener = (toast: ToastMessage) => {
      setToasts(prev => [...prev, toast])
    }
    toastListeners.push(listener)

    return () => {
      const index = toastListeners.indexOf(listener)
      if (index > -1) {
        toastListeners.splice(index, 1)
      }
    }
  }, [])

  const removeToast = (id: string) => {
    setToasts(prev => prev.filter(toast => toast.id !== id))
  }

  return (
    <div className="toast-container">
      {toasts.map(toast => (
        <Toast
          key={toast.id}
          message={toast.message}
          type={toast.type}
          duration={toast.duration}
          onClose={() => removeToast(toast.id)}
        />
      ))}
    </div>
  )
}

export default ToastContainer
