import * as signalR from '@microsoft/signalr'

const HUB_URL = import.meta.env.VITE_HUB_URL || 'https://localhost:60830/moderationHub'

let connection: signalR.HubConnection | null = null

export const connectSignalR = (): signalR.HubConnection => {
  if (connection) {
    return connection
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect()
    .build()

  connection.start().catch(err => {
    console.error('SignalR connection error:', err)
  })

  return connection
}

export const disconnectSignalR = () => {
  if (connection) {
    connection.stop()
    connection = null
  }
}

export interface ModerationResult {
  contentId: string
  decision: string
  score: number
  status: string
}

export const onModerationResult = (
  callback: (result: ModerationResult) => void
) => {
  const hub = connectSignalR()
  hub.on('ModerationResult', (contentId: string, decision: string, score: number, status: string) => {
    callback({ contentId, decision, score, status })
  })
}

export default { connectSignalR, disconnectSignalR, onModerationResult }
