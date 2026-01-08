# Vodič za testiranje Content Moderation Agent-a

## 1. Pokretanje aplikacije

### Korak 1: Restore paketa
```bash
cd "C:\Users\HOME\Desktop\AI Agent"
dotnet restore
```

### Korak 2: Build projekta
```bash
dotnet build
```

### Korak 3: Pokreni aplikaciju
```bash
cd src\AiAgents.ContentModerationAgent.Web
dotnet run
```

Aplikacija će se pokrenuti na:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

## 2. Pristup Swagger UI

Nakon što aplikacija pokrene, otvori browser i idi na:
```
https://localhost:5001/swagger
```

Ovdje možeš vidjeti sve API endpoints i testirati ih direktno.

## 3. Testiranje API-ja

### 3.1. Kreiranje novog sadržaja (komentar/post/poruka)

**Endpoint**: `POST /api/content`

**Primjer request body** (u Swagger UI):
```json
{
  "type": 1,
  "text": "This is a test comment that should be moderated",
  "authorUsername": "test_user",
  "threadId": null
}
```

**Tipovi**:
- `1` = Comment
- `2` = Post  
- `3` = Message

**Response**:
```json
{
  "contentId": "guid-here",
  "status": "Queued"
}
```

### 3.2. Provjera pending review queue

**Endpoint**: `GET /api/content/pending-review`

Vraća sve komentare koji su u statusu `PendingReview` (agent nije siguran i traži moderator review).

### 3.3. Dati feedback (gold label)

**Endpoint**: `POST /api/review/{contentId}/review`

**Primjer request body**:
```json
{
  "goldLabel": 1,
  "correctDecision": true,
  "feedback": "Agent was correct",
  "moderatorId": null
}
```

**Gold Label vrijednosti**:
- `1` = Allow
- `2` = Review
- `3` = Block

## 4. Praćenje agenta u realnom vremenu

### SignalR Hub

Agent automatski emituje evente kroz SignalR kada procesira komentare.

**Hub URL**: `/moderationHub`

**Event**: `ModerationResult`

**Primjer JavaScript klijenta** (možeš testirati u browser console):
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/moderationHub")
    .build();

connection.on("ModerationResult", (contentId, decision, score, status) => {
    console.log(`Content ${contentId}: ${decision} (Score: ${score}, Status: ${status})`);
});

connection.start().then(() => {
    console.log("Connected to SignalR hub");
});
```

## 5. Test scenariji

### Scenarij 1: Čist komentar (trebao bi biti Allow)
```json
POST /api/content
{
  "type": 1,
  "text": "Great article! Very informative.",
  "authorUsername": "trusted_user"
}
```

### Scenarij 2: Spam komentar (trebao bi biti Block)
```json
POST /api/content
{
  "type": 1,
  "text": "SPAM SPAM SPAM BUY NOW CLICK HERE",
  "authorUsername": "new_user"
}
```

### Scenarij 3: Granični slučaj (trebao bi biti Review)
```json
POST /api/content
{
  "type": 1,
  "text": "I'm not sure about this...",
  "authorUsername": "new_user"
}
```

## 6. Provjera baze podataka

Možeš provjeriti šta se dešava u bazi podataka:

### SQL Server Management Studio ili Azure Data Studio

**Connection String** (iz appsettings.json):
```
Server=(localdb)\mssqllocaldb;Database=ContentModerationDb;Trusted_Connection=True;TrustServerCertificate=True
```

### Važne tabele:

1. **Contents** - Svi komentari/postovi/poruke
   ```sql
   SELECT * FROM Contents ORDER BY CreatedAt DESC
   ```

2. **Predictions** - Sve predikcije agenta
   ```sql
   SELECT * FROM Predictions ORDER BY CreatedAt DESC
   ```

3. **Reviews** - Feedback od moderatora
   ```sql
   SELECT * FROM Reviews WHERE GoldLabel IS NOT NULL
   ```

4. **SystemSettings** - Trenutni pragovi i postavke
   ```sql
   SELECT * FROM SystemSettings
   ```

5. **ModelVersions** - Verzije ML modela
   ```sql
   SELECT * FROM ModelVersions ORDER BY Version DESC
   ```

## 7. Kako agent radi

### Automatski proces:

1. **Background Service** (`ModerationAgentBackgroundService`) kontinuirano:
   - Uzima komentare iz queue-a (Status=Queued)
   - Agent ih procesira (Sense → Think → Act)
   - Postavlja status (Approved/PendingReview/Blocked)
   - Emituje SignalR event

2. **Retrain Agent** (`RetrainAgentBackgroundService`) svakih 5 minuta:
   - Provjerava da li ima dovoljno novih gold labels (default: 100)
   - Ako da, trenira novi model
   - Aktivira novi model ako je bolji

3. **Threshold Update Agent** (`ThresholdUpdateAgentBackgroundService`) svakih sat vremena:
   - Analizira feedback metrike
   - Ažurira pragove ako je potrebno

### Ručno testiranje:

1. Kreiraj komentar kroz API
2. Sačekaj nekoliko sekundi (agent će ga automatski procesirati)
3. Provjeri status kroz API ili bazu podataka
4. Ako je u PendingReview, daj feedback kroz Review API
5. Agent će učiti iz feedbacka

## 8. Debugging

### Logovi

Agent logira sve akcije. U konzoli ćeš vidjeti:
```
Processed content {ContentId}: Allow (Score: 0.25)
Processed content {ContentId}: Block (Score: 0.85)
```

### Provjera da li agent radi

1. Kreiraj komentar
2. Provjeri u bazi da li je status promijenjen iz `Queued` u `Approved`/`PendingReview`/`Blocked`
3. Provjeri da li postoji `Prediction` zapis

## 9. Troubleshooting

### Problem: Agent ne procesira komentare
- Provjeri da li Background Service radi (logovi u konzoli)
- Provjeri da li ima komentara sa Status=Queued u bazi

### Problem: ML model ne radi
- Provjeri da li postoji `models` folder
- Provjeri logove za ML greške

### Problem: Baza podataka ne postoji
- Aplikacija automatski kreira bazu pri prvom pokretanju
- Provjeri connection string u appsettings.json

## 10. Napredno testiranje

### Testiranje feedback loop-a:

1. Kreiraj 100+ komentara
2. Daj feedback za sve (gold labels)
3. Sačekaj da Retrain Agent pokrene retraining (5 minuta)
4. Provjeri da li je kreiran novi ModelVersion u bazi

### Testiranje adaptivnih pragova:

1. Kreiraj komentare i daj feedback
2. Provjeri da li ThresholdUpdateAgent mijenja pragove (svakih sat vremena)
3. Provjeri SystemSettings tabelu

## 11. Primjer kompletnog test flow-a

```bash
# 1. Pokreni aplikaciju
cd src\AiAgents.ContentModerationAgent.Web
dotnet run

# 2. U drugom terminalu, testiraj API (ili koristi Swagger UI)
# Kreiraj komentar
curl -X POST https://localhost:5001/api/content \
  -H "Content-Type: application/json" \
  -d '{"type":1,"text":"Test comment","authorUsername":"test_user"}'

# Provjeri pending review
curl https://localhost:5001/api/content/pending-review

# Daj feedback
curl -X POST https://localhost:5001/api/review/{contentId}/review \
  -H "Content-Type: application/json" \
  -d '{"goldLabel":1,"correctDecision":true}'
```

## 12. Korisni linkovi

- Swagger UI: `https://localhost:5001/swagger`
- SignalR Hub: `https://localhost:5001/moderationHub`
- Health check (ako dodamo): `https://localhost:5001/health`
