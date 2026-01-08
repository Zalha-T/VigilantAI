# Frontend Setup Guide

## Pokretanje Frontend Aplikacije

### 1. Install Node.js

Provjeri da li imaš Node.js instaliran:
```bash
node --version
npm --version
```

Ako nemaš, preuzmi sa: https://nodejs.org/

### 2. Install Dependencies

```bash
cd frontend
npm install
```

### 3. Konfiguracija

Kreiraj `.env` file u `frontend` folderu (ili koristi `.env.example`):

```env
VITE_API_URL=https://localhost:60830/api
VITE_HUB_URL=https://localhost:60830/moderationHub
```

**VAŽNO:** Ako backend koristi drugačiji port, promijeni URL-ove u `.env` file-u.

### 4. Pokreni Backend

Prije pokretanja frontenda, pokreni backend:
```bash
cd src/AiAgents.ContentModerationAgent.Web
dotnet run
```

Backend treba biti dostupan na `https://localhost:60830`

### 5. Pokreni Frontend

U novom terminalu:
```bash
cd frontend
npm run dev
```

Frontend će biti dostupan na `http://localhost:3000`

## Troubleshooting

### Problem: CORS errors

Ako vidiš CORS greške u browser konzoli, provjeri da li backend podržava CORS. Možda treba dodati CORS middleware u `Program.cs`.

### Problem: SignalR connection failed

Provjeri:
1. Da li je backend pokrenut
2. Da li je port ispravan u `.env` file-u
3. Browser konzola za detaljne greške

### Problem: API calls fail

Provjeri:
1. Da li je backend pokrenut
2. Da li je `VITE_API_URL` ispravan u `.env` file-u
3. Browser Network tab da vidiš što se dešava sa requestovima

## Features

### Dashboard (`/`)
- Pregled svih komentara
- Filter po statusu
- Paginacija
- Real-time updates kroz SignalR
- Klik na komentar → detalji

### Review Queue (`/review`)
- Lista komentara koji čekaju review
- Quick actions: Allow/Review/Block
- Automatski refresh svakih 5 sekundi

### Content Details (`/content/:id`)
- Detalji komentara
- Agent prediction scores
- Labels (Spam, Toxic, Hate, Offensive)
- Review history

## Build za Produkciju

```bash
npm run build
```

Build fajlovi će biti u `dist` folderu. Možeš ih servirati sa bilo kojim web serverom (nginx, IIS, itd.).
