# Retraining vs Wordlist - Detaljno Objašnjenje

## 🎯 Kratak Odgovor

**Retraining i Wordlist su DVIJE ODRVOJENE STVARI:**

- **Wordlist** = Ručno dodavanje riječi koje se odmah koriste za detekciju
- **Retraining** = Automatsko učenje ML modela iz tvojih feedbackova (Allow/Block)

**NEMA direktne veze između njih!**

---

## 📝 Šta je Wordlist?

### Kako radi:
1. **Ručno dodaješ riječi** kroz UI (Settings → Wordlist)
2. **Agent odmah koristi te riječi** za detekciju
3. **Nema potrebe za retraining** - radi odmah

### Gdje se koristi:
- U `MlNetContentClassifier.PredictAsync()` metodi
- Agent provjerava da li tekst sadrži riječi iz wordlista
- Kombinira se sa base keywords (hardcoded u kodu)

### Primjer:
```
1. Dodaješ "slur-word" u wordlist (kategorija: "slur")
2. Agent vidi komentar: "You are a slur-word"
3. Agent odmah detektira "slur-word" → blokira
```

---

## 🤖 Šta je Retraining?

### Kako radi:
1. **Daješ feedback** na komentare (Allow/Block u Review Queue)
2. **Gold labels se spremaju** u bazu (Reviews tabela)
3. **Nakon X novih gold labels** (npr. 10) → retraining se triggera
4. **ML model se trenira** sa svim gold labels iz baze
5. **Novi model uči pattern-e** iz tvojih feedbackova

### Gdje se koristi:
- Trenira se ML model u `MlNetContentClassifier.TrainAsync()`
- **ALI**: Trenutno ML model **NIJE KORIŠTEN** za predikcije!
- Agent i dalje koristi keyword-based heuristiku (wordlist + base keywords)

### Primjer:
```
1. Kreiraš 10 komentara
2. Daješ feedback na svaki (Allow/Block)
3. Retraining se triggera
4. ML model se trenira sa tim 10 primjera
5. Model uči: "fuck you" → Block, "hello" → Allow
```

---

## 🔄 Razlika između Wordlist i Retraining

| Karakteristika | Wordlist | Retraining |
|---------------|----------|------------|
| **Kako se mijenja** | Ručno kroz UI | Automatski iz feedbackova |
| **Kada se primjenjuje** | Odmah | Nakon retraininga |
| **Šta mijenja** | Lista riječi za provjeru | ML model (ali trenutno se ne koristi) |
| **Potrebno za rad** | Ništa | Minimum 10 gold labels |
| **Dodaje riječi?** | ✅ DA | ❌ NE |
| **Uči pattern-e?** | ❌ NE | ✅ DA |

---

## ⚠️ Važno - Trenutno Stanje

### Šta se ZAPRAVO koristi za predikcije:

**Agent trenutno koristi:**
1. ✅ **Base keywords** (hardcoded u kodu)
2. ✅ **Wordlist** (dinamičke riječi iz baze)
3. ✅ **Image classification** (ako ima sliku)
4. ✅ **Context factors** (author reputation, time, etc.)

**Agent NE koristi:**
- ❌ **ML model iz retraininga** (trenutno se ne koristi!)

### Zašto?

ML model iz retraininga trenutno **NIJE INTEGRIRAN** u `PredictAsync()` metodu. Agent koristi keyword-based heuristiku umjesto ML modela.

---

## 💡 Kada koristiti šta?

### Koristi Wordlist kada:
- ✅ Vidiš novu riječ koja nije blokirana
- ✅ Želiš odmah blokirati određene riječi
- ✅ Imaš specifične riječi za svoju domenu
- ✅ Želiš brzu, direktnu kontrolu

### Retraining će biti koristan kada:
- ✅ ML model bude integrisan u predikcije
- ✅ Imaš puno feedbackova (100+)
- ✅ Želiš da agent uči generalne pattern-e
- ✅ Želiš da agent prepoznaje kontekst, ne samo riječi

---

## 🔍 Kako provjeriti šta se koristi?

### Provjeri Wordlist:
```sql
SELECT * FROM BlockedWords WHERE IsActive = 1
```

### Provjeri Retraining:
```sql
-- Koliko gold labels imaš?
SELECT COUNT(*) FROM Reviews WHERE GoldLabel IS NOT NULL

-- Koje verzije modela su trenirane?
SELECT * FROM ModelVersions ORDER BY Version DESC

-- Kada je zadnji retraining?
SELECT LastRetrainDate FROM SystemSettings
```

---

## 📊 Primjer Scenarija

### Scenario 1: Dodavanje riječi u Wordlist
```
1. Vidiš komentar: "You are a slur-word"
2. Agent ga ne blokira (riječ nije u wordlistu)
3. Otvoriš Wordlist → dodaješ "slur-word"
4. Sledeći komentar sa "slur-word" → odmah blokiran ✅
```

### Scenario 2: Retraining
```
1. Kreiraš 15 komentara
2. Daješ feedback na svaki (Allow/Block)
3. Retraining se triggera (15 >= 10)
4. ML model se trenira sa 15 primjera
5. Model uči pattern-e, ALI se ne koristi za predikcije (još)
```

### Scenario 3: Kombinacija
```
1. Dodaješ "slur-word" u wordlist → odmah radi
2. Daješ feedback na komentare → retraining se triggera
3. ML model se trenira, ALI agent i dalje koristi wordlist za predikcije
```

---

## 🎓 Zaključak

**Wordlist i Retraining su odvojeni sistemi:**

- **Wordlist** = Brza, direktna kontrola kroz ručno dodavanje riječi
- **Retraining** = Dugoročno učenje iz feedbackova (trenutno se ne koristi)

**Za sada, wordlist je glavni način kontrole!** Retraining trenira model, ali taj model se ne koristi za predikcije.
