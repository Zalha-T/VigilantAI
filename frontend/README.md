# VigilantAI - Frontend

React + TypeScript frontend aplikacija za VigilantAI.

## Features

- 📊 Dashboard sa pregledom svih komentara
- 🔍 Review Queue za komentare koji čekaju review
- 📝 Detalji komentara sa scores i labels
- ✅ Feedback forma za moderatore
- 🔄 Real-time updates kroz SignalR

## Pokretanje

### 1. Install dependencies

```bash
cd frontend
npm install
```

### 2. Pokreni development server

```bash
npm run dev
```

Aplikacija će biti dostupna na `http://localhost:3000`

## Konfiguracija

Backend URL se može konfigurirati kroz environment varijable:

```env
VITE_API_URL=https://localhost:60830/api
VITE_HUB_URL=https://localhost:60830/moderationHub
```

## Build za produkciju

```bash
npm run build
```

Build fajlovi će biti u `dist` folderu.

## Struktura

```
frontend/
├── src/
│   ├── pages/
│   │   ├── Dashboard.tsx       # Glavni dashboard
│   │   ├── ReviewQueue.tsx     # Review queue
│   │   └── ContentDetails.tsx # Detalji komentara
│   ├── services/
│   │   ├── api.ts              # API servis
│   │   └── signalr.ts          # SignalR connection
│   ├── components/
│   │   └── ReviewForm.tsx      # Review form komponenta
│   ├── App.tsx                 # Main app komponenta
│   └── main.tsx                # Entry point
└── package.json
```
