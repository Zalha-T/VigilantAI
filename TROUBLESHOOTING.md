# Troubleshooting Guide - Content Moderation Agent

## Problem: Komentari se ne procesiraju (status ostaje Queued ili Processing)

### Simptomi:
- Komentari imaju `status: 1` (Queued) ili `status: 2` (Processing)
- `prediction: null`
- Svi labels su `false`
- `processedAt: null`

### Rješenja:

#### 1. Provjeri da li Background Service radi

U konzoli gdje aplikacija radi, trebao bi vidjeti:
```
ModerationAgentBackgroundService started
```

Ako ne vidiš ovu poruku, Background Service se nije pokrenuo.

**Rješenje:**
- Restart aplikacije
- Provjeri da li ima grešaka pri pokretanju

#### 2. Provjeri logove za greške

U konzoli traži poruke poput:
```
✗ Error in moderation agent background service: ...
```

Ako vidiš greške, pošalji ih da vidim što se dešava.

#### 3. Reset zaglavljene komentare

```bash
POST https://localhost:60830/api/content/reset-stuck
```

Ovaj endpoint resetuje komentare koji su zaglavljeni u Processing statusu.

#### 4. Ručno trigger procesiranje

Ako Background Service ne radi, možeš ručno trigger procesiranje kroz API:

```bash
# Kreiraj novi komentar
POST /api/content
{
  "type": 1,
  "text": "test",
  "authorUsername": "test"
}

# Background Service bi trebao automatski procesirati
```

#### 5. Provjeri da li ima komentara u queue-u

```bash
GET /api/content?status=1
```

Ako vidiš komentare sa `status: 1`, znači da su u queue-u i čekaju procesiranje.

---

## Problem: Background Service ne radi

### Simptomi:
- Nema logova u konzoli
- Komentari se ne procesiraju

### Rješenja:

#### 1. Provjeri Program.cs

Background Service se registrira u `Program.cs`:
```csharp
builder.Services.AddHostedService<ModerationAgentBackgroundService>();
```

Provjeri da li je ova linija prisutna.

#### 2. Restart aplikacije

Ponekad Background Service ne startuje pravilno. Restart aplikacije često rješava problem.

#### 3. Provjeri dependency injection

Background Service treba:
- `IServiceProvider`
- `IHubContext<ModerationHub>`
- `ILogger<ModerationAgentBackgroundService>`

Provjeri da li su svi registrirani u `Program.cs`.

---

## Problem: ML Classifier ne radi

### Simptomi:
- Exception u logovima vezan za ML
- Prediction je null

### Rješenja:

#### 1. Provjeri da li models folder postoji

ML classifier koristi `models` folder. Provjeri da li postoji u root direktoriju Web projekta.

#### 2. Koristi heuristički classifier

Ako ML model ne radi, agent koristi keyword-based heuristiku:
- Spam keywords: "spam", "buy now", "click here", "deal", "offer"
- Toxic keywords: "fuck", "bitch", "idiot", "hate"
- Hate keywords: "hate", "kill", "die"
- Offensive keywords: "fuck", "bitch", "damn", "shit"

---

## Problem: Komentari se procesiraju ali labels su svi false

### Simptomi:
- Komentari imaju `prediction` objekt
- Ali `labels.isSpam`, `labels.isToxic`, itd. su svi `false`

### Rješenja:

#### 1. Provjeri scores u prediction objektu

```bash
GET /api/content/{id}
```

Provjeri `prediction.spamScore`, `prediction.toxicScore`, itd.

Labels se generišu na osnovu scores:
- `isSpam = true` ako je `spamScore > 0.5`
- `isToxic = true` ako je `toxicScore > 0.5`
- itd.

Ako su scores niski (< 0.5), labels će biti `false`.

#### 2. Provjeri thresholds

```bash
# Provjeri SystemSettings u bazi
SELECT * FROM SystemSettings
```

Thresholds određuju kada je nešto problematično:
- `AllowThreshold`: < ovaj prag = Allow
- `ReviewThreshold`: između Allow i Review = Review
- `BlockThreshold`: > ovaj prag = Block

---

## Debugging Tips

### 1. Provjeri logove u konzoli

Traži:
- `✓ Processed content` - uspješno procesiranje
- `✗ Error` - greška
- `No content in queue` - nema posla

### 2. Provjeri bazu podataka

```sql
-- Provjeri status komentara
SELECT Status, COUNT(*) 
FROM Contents 
GROUP BY Status

-- Provjeri da li ima predikcija
SELECT COUNT(*) FROM Predictions

-- Provjeri zaglavljene komentare
SELECT * FROM Contents 
WHERE Status = 2 AND CreatedAt < DATEADD(minute, -5, GETUTCDATE())
```

### 3. Testiraj ručno

```bash
# 1. Kreiraj komentar
POST /api/content
{
  "type": 1,
  "text": "fuck you",
  "authorUsername": "test"
}

# 2. Sačekaj 5 sekundi

# 3. Provjeri status
GET /api/content/{contentId}

# Trebao bi vidjeti prediction i labels
```

---

## Česti problemi i rješenja

### Problem: "Author not found" exception
**Rješenje:** Provjeri da li Author postoji u bazi prije kreiranja Content-a.

### Problem: Context već postoji
**Rješenje:** Popravljeno - ContextService sada provjerava da li Context već postoji.

### Problem: Background Service ne startuje
**Rješenje:** Provjeri da li je `AddHostedService` pozvan u `Program.cs`.

### Problem: Komentari se procesiraju ali status ostaje Processing
**Rješenje:** Provjeri da li `ScoringService.ScoreAndDecideAsync` ažurira status. Trebao bi ažurirati u `SaveChangesAsync`.

---

## Ako ništa ne pomaže

1. **Restart aplikacije** - često rješava probleme
2. **Reset baze** - obriši bazu i kreiraj ponovo (`EnsureCreated`)
3. **Provjeri logove** - šalji greške da vidim što se dešava
4. **Provjeri da li Background Service radi** - traži "ModerationAgentBackgroundService started" u logovima
