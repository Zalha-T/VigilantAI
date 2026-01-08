# Retraining - Detaljno Objašnjenje

## 🔄 Kako Retraining Radi

### 1. Kada se pokreće?

**RetrainAgentBackgroundService** radi kontinuirano u pozadini:
- Provjerava svakih 5 minuta da li treba retrain
- Pokreće se automatski kada backend radi
- Ne trebaš ništa ručno pokrenuti

### 2. Kada se retrain aktivira?

Retraining se aktivira kada:
- `NewGoldSinceLastTrain >= RetrainThreshold` (npr. 11 >= 10)
- `RetrainingEnabled = true`
- Ima minimum 10 ukupnih gold labels u bazi

### 3. Šta retraining radi?

**Retraining NE dodaje riječi u wordlist!**

Retraining trenira ML model:

1. **Skuplja sve gold labels** iz Reviews tabele
2. **Trenira novi ML model** sa tim podacima:
   - Input: Tekst komentara (npr. "fuck you")
   - Output: Gold label (Allow/Block) koji si dao
3. **Kreira novu verziju modela** (ModelVersion)
4. **Aktivira novi model** (stari se deaktivira)
5. **Resetuje counter**: `NewGoldSinceLastTrain = 0`

### 4. Šta retraining NE radi?

- ❌ NE dodaje riječi u wordlist
- ❌ NE mijenja threshold-e
- ❌ NE mijenja heuristiku
- ✅ SAMO trenira ML model da bolje prepoznaje pattern-e

### 5. Razlika između Wordlist i Retraining

**Wordlist (ručno dodavanje riječi):**
- Dodaješ riječi kroz UI (npr. "slur-word")
- Agent odmah koristi te riječi za detekciju
- Ne treba retraining

**Retraining (ML model učenje):**
- Agent uči iz tvojih feedbackova (Allow/Block)
- Trenira ML model da prepozna pattern-e
- Ne dodaje konkretne riječi, već uči generalne pattern-e

### 6. Primjer

**Scenario:**
1. Kreiraš komentar: "fuck you"
2. Agent ga šalje u Review Queue
3. Ti klikneš Block
4. Gold label se sprema
5. Nakon 10+ gold labels → retraining
6. Novi model uči: "fuck you" → Block
7. Sada kada vidi sličan tekst → automatski Block

**Ali:**
- Retraining NE dodaje "fuck" u wordlist
- Wordlist se mijenja ručno kroz UI
- Retraining uči generalne pattern-e, ne konkretne riječi

## ⚠️ Zašto retraining možda ne radi?

1. **Nema dovoljno ukupnih gold labels**:
   - Treba minimum 10 ukupno u bazi
   - Ne samo 10 novih, već 10 ukupno

2. **RetrainingEnabled = false**:
   - Provjeri u Settings

3. **Greška u retraining procesu**:
   - Provjeri backend logove
   - Možda ima exception

## 🔍 Kako provjeriti?

1. **Backend logovi**:
   ```
   Retraining check: NewGoldSinceLastTrain=11, RetrainThreshold=10, RetrainingEnabled=True
   Error in retrain agent background service: Not enough gold labels for training. Need at least 10, have X
   ```

2. **Baza podataka**:
   ```sql
   SELECT COUNT(*) FROM Reviews WHERE GoldLabel IS NOT NULL
   -- Treba biti >= 10
   
   SELECT * FROM ModelVersions ORDER BY Version DESC
   -- Provjeri da li ima novih verzija
   ```

3. **Settings stranica**:
   - Provjeri "New Gold Labels Since Last Train"
   - Provjeri "Last Retrain Date"

## 💡 Kako ubrzati retraining?

1. **Smanji RetrainThreshold** (npr. na 5)
2. **Daj više feedbackova** dok ne budeš imao 10+ ukupno
3. **Provjeri da li RetrainingEnabled = true**
