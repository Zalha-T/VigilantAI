# Kako Agent Uči - Detaljno Objašnjenje

## 🔄 Feedback Loop i Učenje

### 1. Kada daš feedback (Allow/Block/Review)

Kada klikneš **Allow** ili **Block** na komentaru u Review Queue:

1. **Feedback se sprema** u `Reviews` tabelu sa `GoldLabel` (tvoja odluka)
2. **Content status se mijenja**:
   - Allow → Status = Approved
   - Block → Status = Blocked
3. **Brojač se povećava**: `SystemSettings.NewGoldSinceLastTrain++`

### 2. Kada se agent retrenira?

Agent se **automatski retrenira** kada:
- `NewGoldSinceLastTrain >= RetrainThreshold` (default: 100)
- RetrainAgentRunner provjerava svakih 5 minuta
- Kada se nakupi 100+ novih gold labels → pokreće se retraining

### 3. Kako agent uči iz feedbacka?

**Retraining proces:**

1. **Skuplja sve gold labels** (sve Reviews sa GoldLabel != null)
2. **Trenira novi ML model** sa tim podacima:
   - Input: Tekst komentara
   - Output: Gold label (Allow/Block)
3. **A/B testiranje**: Testira novi model vs stari
4. **Aktivacija**: Ako je novi model bolji → aktivira se

### 4. Šta se dešava sa istim komentarom?

**Scenario: Napišeš isti komentar ponovo**

1. **Agent procesira** komentar sa trenutnim modelom
2. **Ako je model naučio** iz prethodnog feedbacka:
   - Isti tekst → sličan score
   - Ako si prethodno blokirao → agent će vjerovatno blokirati i ovaj
3. **Ako model nije još retreniran**:
   - Agent koristi stari model
   - Možda donese istu grešku
   - Ali nakon retraining-a → učit će iz prethodnog feedbacka

### 5. Adaptivni Pragovi

**ThresholdUpdateRunner** (svakih sat vremena):

1. **Analizira feedback metrike**:
   - False Positive Rate (blokirao dobar content)
   - False Negative Rate (propustio loš content)
2. **Ažurira pragove**:
   - Ako ima previše false positives → poveća pragove (manje strog)
   - Ako ima previše false negatives → smanji pragove (striktniji)

## 📊 Primjer Učenja

### Scenario 1: Prvi put vidiš "fuck you"

1. Agent procesira → Score: 0.35 → **PendingReview**
2. Ti daš feedback → **Block** (gold label)
3. Gold label counter: 1/100

### Scenario 2: Nakon 100+ feedbackova

1. RetrainAgentRunner detektira: 100+ gold labels
2. Trenira novi model sa svim gold labels
3. Novi model aktiviran
4. Sada kada vidi "fuck you" → Score: 0.75 → **Block** (direktno, bez review)

### Scenario 3: Isti komentar ponovo

1. Napišeš "fuck you" ponovo
2. Agent koristi novi model (naučio iz prethodnog feedbacka)
3. Score: 0.75 → **Block** (automatski, bez review)

## ⚙️ Konfiguracija

**SystemSettings** kontrolira učenje:

- `RetrainThreshold`: 100 (broj gold labels potrebnih za retraining)
- `NewGoldSinceLastTrain`: trenutni broj novih gold labels
- `RetrainingEnabled`: true/false (možeš onemogućiti)

## 🔍 Provjera da li agent uči

1. **Provjeri u bazi**:
   ```sql
   SELECT * FROM SystemSettings
   -- Provjeri NewGoldSinceLastTrain
   
   SELECT * FROM ModelVersions ORDER BY Version DESC
   -- Provjeri da li ima novih verzija modela
   ```

2. **Provjeri logove**:
   - Traži: "Model retraining completed"
   - Traži: "Thresholds updated based on feedback"

## 💡 Važne Napomene

- **Agent ne uči odmah**: Treba 100+ feedbackova prije retraining-a
- **Retraining je spor**: Može trajati nekoliko minuta
- **A/B testiranje**: Novi model se aktivira samo ako je bolji
- **Pragovi se adaptiraju**: Mijenjaju se na osnovu false positives/negatives

## 🎯 Kako ubrzati učenje?

1. **Smanji RetrainThreshold** (npr. na 50 umjesto 100)
2. **Daj više feedbackova** → brže će se nakupiti 100
3. **Provjeri da li RetrainingEnabled = true**
