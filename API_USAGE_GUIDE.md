# Vodič za korištenje Content Moderation Agent API-ja

## 📋 Pregled API Endpoints

### Base URL
```
https://localhost:60830/api
```
*(Port može biti drugačiji - provjeri u konzoli kada pokreneš aplikaciju)*

---

## 1️⃣ Kreiranje novog sadržaja (komentar/post/poruka)

### Endpoint
```
POST /api/content
```

### Request Body
```json
{
  "type": 1,
  "text": "Tekst komentara ili posta",
  "authorUsername": "ime_korisnika",
  "threadId": null
}
```

### Tipovi (type):
- `1` = Comment (komentar)
- `2` = Post (post)
- `3` = Message (poruka)

### Primjer u cURL:
```bash
curl -X POST https://localhost:60830/api/content \
  -H "Content-Type: application/json" \
  -d '{
    "type": 1,
    "text": "This is a test comment with bad words",
    "authorUsername": "test_user"
  }'
```

### Response:
```json
{
  "contentId": "a34ed113-d214-4362-96b2-d58b52fafceb",
  "status": "Queued"
}
```

**Šta se dešava:**
- Komentar se dodaje u queue
- Agent automatski procesira komentar u pozadini (2-3 sekunde)
- Status se mijenja u: `Approved`, `PendingReview`, ili `Blocked`

---

## 2️⃣ Dohvati SVE contente sa labelima

### Endpoint
```
GET /api/content
```

### Query Parameters:
- `status` (opcionalno): Filter po statusu
  - `1` = Queued
  - `2` = Processing
  - `3` = Approved
  - `4` = PendingReview
  - `5` = Blocked
- `page` (opcionalno): Broj stranice (default: 1)
- `pageSize` (opcionalno): Broj rezultata po stranici (default: 50)

### Primjeri:

#### Dohvati sve contente:
```bash
GET https://localhost:60830/api/content
```

#### Dohvati samo blokirane:
```bash
GET https://localhost:60830/api/content?status=5
```

#### Dohvati sa paginacijom:
```bash
GET https://localhost:60830/api/content?page=1&pageSize=20
```

### Response:
```json
{
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3,
  "data": [
    {
      "id": "a34ed113-d214-4362-96b2-d58b52fafceb",
      "text": "This is a test comment",
      "type": 1,
      "status": 5,
      "createdAt": "2026-01-08T17:00:00Z",
      "processedAt": "2026-01-08T17:00:03Z",
      "author": {
        "username": "test_user",
        "reputationScore": 50
      },
      "prediction": {
        "decision": 3,
        "finalScore": 0.85,
        "spamScore": 0.9,
        "toxicScore": 0.7,
        "hateScore": 0.2,
        "offensiveScore": 0.5,
        "confidence": 3
      },
      "review": {
        "goldLabel": 1,
        "correctDecision": false,
        "feedback": "Agent was too strict"
      },
      "labels": {
        "isSpam": true,
        "isToxic": true,
        "isHate": false,
        "isOffensive": true,
        "isProblematic": true,
        "agentDecision": 3,
        "humanLabel": 1
      }
    }
  ]
}
```

### Objašnjenje Labela:

**Labels objekt sadrži:**
- `isSpam`: true ako je spam score > 0.5
- `isToxic`: true ako je toxic score > 0.5
- `isHate`: true ako je hate score > 0.5
- `isOffensive`: true ako je offensive score > 0.5
- `isProblematic`: true ako je final score > 0.5 (ukupno problematičan)
- `agentDecision`: Odluka agenta (1=Allow, 2=Review, 3=Block)
- `humanLabel`: Gold label od moderatora (ako postoji)

---

## 3️⃣ Dohvati detalje jednog contenta

### Endpoint
```
GET /api/content/{id}
```

### Primjer:
```bash
GET https://localhost:60830/api/content/a34ed113-d214-4362-96b2-d58b52fafceb
```

### Response:
```json
{
  "id": "a34ed113-d214-4362-96b2-d58b52fafceb",
  "text": "This is a test comment",
  "type": 1,
  "status": 5,
  "createdAt": "2026-01-08T17:00:00Z",
  "processedAt": "2026-01-08T17:00:03Z",
  "author": {
    "username": "test_user",
    "reputationScore": 50,
    "accountAgeDays": 5,
    "previousViolations": 0
  },
  "predictions": [
    {
      "id": "prediction-id",
      "decision": 3,
      "finalScore": 0.85,
      "spamScore": 0.9,
      "toxicScore": 0.7,
      "hateScore": 0.2,
      "offensiveScore": 0.5,
      "confidence": 3,
      "contextFactors": "{\"authorReputation\":0.5,\"timeOfDay\":17}",
      "createdAt": "2026-01-08T17:00:03Z"
    }
  ],
  "reviews": [
    {
      "id": "review-id",
      "goldLabel": 1,
      "correctDecision": false,
      "feedback": "Agent was too strict",
      "moderatorId": null,
      "createdAt": "2026-01-08T17:07:57Z",
      "reviewedAt": "2026-01-08T17:07:57Z"
    }
  ],
  "context": {
    "authorReputation": 0.5,
    "threadSentiment": 0.0,
    "engagementLevel": 0.5,
    "timeOfDay": 17,
    "dayOfWeek": 3
  }
}
```

---

## 4️⃣ Dohvati komentare koji čekaju review

### Endpoint
```
GET /api/content/pending-review
```

### Primjer:
```bash
GET https://localhost:60830/api/content/pending-review
```

### Response:
```json
[
  {
    "id": "content-id",
    "text": "Granični slučaj komentara",
    "type": 1,
    "author": "test_user",
    "prediction": {
      "decision": 2,
      "finalScore": 0.52,
      "confidence": 1
    },
    "createdAt": "2026-01-08T17:00:00Z"
  }
]
```

---

## 5️⃣ Daj feedback (gold label) za komentar

### Endpoint
```
POST /api/review/{contentId}/review
```

### Request Body:
```json
{
  "goldLabel": 1,
  "correctDecision": true,
  "feedback": "Agent was correct",
  "moderatorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Gold Label vrijednosti:
- `1` = Allow (komentar je OK)
- `2` = Review (treba dodatni review)
- `3` = Block (komentar treba blokirati)

### correctDecision:
- `true` = Agent je bio u pravu
- `false` = Agent je pogriješio

### Primjer:
```bash
curl -X POST https://localhost:60830/api/review/a34ed113-d214-4362-96b2-d58b52fafceb/review \
  -H "Content-Type: application/json" \
  -d '{
    "goldLabel": 3,
    "correctDecision": true,
    "feedback": "This content contains hate speech",
    "moderatorId": null
  }'
```

### Response:
```json
{
  "reviewId": "6e821b40-78c2-46f2-a751-ba6def7db654"
}
```

---

## 📊 Statusi i Odluke

### ContentStatus (status komentara):
- `1` = Queued (čeka procesiranje)
- `2` = Processing (u procesu)
- `3` = Approved (odobren)
- `4` = PendingReview (čeka review)
- `5` = Blocked (blokiran)

### ModerationDecision (odluka agenta/moderatora):
- `1` = Allow (dozvoli)
- `2` = Review (potreban review)
- `3` = Block (blokiraj)

### ConfidenceLevel (pouzdanost agenta):
- `1` = Low (niska)
- `2` = Medium (srednja)
- `3` = High (visoka)

---

## 🔍 Praktični primjeri korištenja

### Scenario 1: Kreiraj komentar i provjeri rezultat

```bash
# 1. Kreiraj komentar
curl -X POST https://localhost:60830/api/content \
  -H "Content-Type: application/json" \
  -d '{
    "type": 1,
    "text": "This is spam spam spam",
    "authorUsername": "spammer"
  }'

# Response: {"contentId": "abc-123", "status": "Queued"}

# 2. Sačekaj 3 sekunde da agent procesira

# 3. Dohvati detalje
curl https://localhost:60830/api/content/abc-123

# 4. Vidiš da je status = 5 (Blocked) i labels.isSpam = true
```

### Scenario 2: Dohvati sve problematične komentare

```bash
# Dohvati sve blokirane komentare
curl "https://localhost:60830/api/content?status=5"

# Filtrirati samo one sa isSpam = true u aplikaciji
```

### Scenario 3: Review workflow

```bash
# 1. Dohvati komentare koji čekaju review
curl https://localhost:60830/api/content/pending-review

# 2. Za svaki komentar, daj feedback
curl -X POST https://localhost:60830/api/review/{contentId}/review \
  -H "Content-Type: application/json" \
  -d '{
    "goldLabel": 1,
    "correctDecision": true,
    "feedback": "OK"
  }'
```

---

## 🛠️ Korištenje u Swagger UI

1. **Pokreni aplikaciju**: `dotnet run` u Web projektu
2. **Otvori Swagger**: `https://localhost:60830/swagger`
3. **Testiraj endpoints**:
   - Klikni na endpoint
   - Klikni "Try it out"
   - Unesi parametre
   - Klikni "Execute"

---

## 📝 Napomene

- **Agent automatski procesira**: Kada kreiraš komentar, agent će ga automatski procesirati u pozadini (2-3 sekunde)
- **Feedback loop**: Kada daš feedback, agent uči iz njega. Nakon 100+ feedbackova, model se retrenira
- **Labels**: Labels se generišu na osnovu ML scores. Ako je score > 0.5, label je `true`
- **Paginacija**: Default je 50 rezultata po stranici. Možeš mijenjati sa `pageSize` parametrom

---

## 🐛 Troubleshooting

### Problem: "Content not found"
- Provjeri da li je ID ispravan
- Provjeri da li je komentar kreiran

### Problem: Agent ne procesira komentare
- Provjeri logove u konzoli
- Provjeri da li Background Service radi
- Provjeri da li ima komentara sa Status=Queued u bazi

### Problem: Labels su svi false
- Provjeri da li agent procesirao komentar (provjeri Prediction)
- Provjeri scores u Prediction objektu

---

## 💡 Tips & Tricks

1. **Filtriranje po labelima**: Koristi `GET /api/content` i filtriraj u aplikaciji po `labels.isSpam`, `labels.isToxic`, itd.

2. **Praćenje feedbacka**: Provjeri `review.humanLabel` vs `labels.agentDecision` da vidiš koliko se agent slaže sa moderatorima

3. **Analiza performansi**: Koristi `correctDecision` u Review objektu da vidiš koliko je agent tačan

4. **Bulk operations**: Za više komentara odjednom, koristi loop u aplikaciji ili PowerShell script
