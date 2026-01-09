# Retraining Guide - Kako Provjeriti i Što Radi

## Kako Provjeriti da li je Retrain Triggeran?

### 1. **Backend Logovi (Najbolji način)**

Kada pokreneš backend, gledaj konzolu za ove poruke:

**Kada se retrain triggera:**
```
🚀 IMMEDIATE RETRAINING TRIGGERED: Threshold reached (10/10)
========== RETRAINING STARTED ==========
Found 15 gold labels for training
Starting model training with 15 gold labels...
Model training completed. Metrics: Accuracy=85.23%, Precision=82.10%, Recall=88.50%, F1Score=85.20%
Creating new model version: v2
Deactivating old model version: v1
Activating new model version: v2
========== RETRAINING COMPLETED ==========
New model version v2 created and ACTIVATED. Previous gold labels count: 10, Reset to 0.
✅ Immediate retraining completed successfully
```

**Ili iz background service (svakih 5 minuta):**
```
Model retraining completed successfully
```

**Ako retrain nije triggeran:**
```
Retraining check: NewGoldSinceLastTrain=7, RetrainThreshold=10, RetrainingEnabled=True
```

### 2. **API Endpoint - Settings**

Pozovi `GET /api/settings` da vidiš status:

```json
{
  "retrainThreshold": 10,
  "newGoldSinceLastTrain": 10,
  "lastRetrainDate": "2026-01-09T14:30:00Z",
  "retrainingEnabled": true,
  "retrainingStatus": {
    "canRetrain": true,
    "progress": "10 / 10",
    "percentage": 100.0
  }
}
```

**Ako `canRetrain: true`** → Retrain bi trebao biti triggeran
**Ako `lastRetrainDate` je nedavno** → Retrain je upravo završen

### 3. **Database - ModelVersions Table**

Provjeri `ModelVersions` tabelu u bazi:
- `Version` - broj verzije (1, 2, 3...)
- `TrainedAt` - kada je treniran
- `IsActive` - da li je aktivan
- `TrainingSampleCount` - koliko gold labels je korišteno
- `Accuracy`, `Precision`, `Recall`, `F1Score` - metrike

## Što Retrain Radi?

### 1. **Prikuplja Gold Labels**
- Uzima SVE reviews sa `GoldLabel != null` iz baze
- Minimum 10 gold labels je potrebno (inače baca grešku)

### 2. **Trenira ML Model**
- Koristi ML.NET `FastTree` algoritam
- Trenira na tekstu iz gold labels
- Kreira features iz teksta (text featurization)
- Trenira binary classification model

### 3. **Kreira Novu Verziju Modela**
- Kreira `ModelVersion` u bazi
- Inkrementira verziju (v1 → v2 → v3...)
- Sprema metrike (Accuracy, Precision, Recall, F1Score)
- Sprema broj training samples

### 4. **Aktivira Novi Model**
- Ako `activate: true`:
  - Deaktivira stare modele (`IsActive = false`)
  - Aktivira novi model (`IsActive = true`)
  - Novi model se koristi za buduće predikcije

### 5. **Resetuje Counter**
- `NewGoldSinceLastTrain` se resetuje na 0
- `LastRetrainDate` se postavlja na trenutno vrijeme

## Kada se Retrain Triggera?

### Immediate (Odmah):
- Kada se submita review i `NewGoldSinceLastTrain >= RetrainThreshold`
- Poziva se direktno iz `ReviewService.UpdateReviewAsync`

### Background Service (Backup):
- Provjerava svakih 5 minuta
- Ako je threshold pređen, triggera retrain
- Backup ako immediate retrain ne uspije

## Važne Napomene

1. **Retrain koristi SVE gold labels** - ne samo nove, već sve iz baze
2. **Minimum 10 gold labels** - ako nema dovoljno, retrain se preskače
3. **Model se trenira na tekstu** - koristi heuristike + ML.NET
4. **Novi model se aktivira automatski** - stari se deaktivira
5. **Counter se resetuje** - nakon retrain-a, počinje od 0

## Troubleshooting

**Problem: Retrain se ne triggera**
- Provjeri: `RetrainingEnabled = true`?
- Provjeri: `NewGoldSinceLastTrain >= RetrainThreshold`?
- Provjeri logove za greške

**Problem: "Not enough gold labels"**
- Treba minimum 10 reviews sa gold labels
- Submitaj više reviews

**Problem: Retrain se triggera ali model nije bolji**
- Provjeri metrike u `ModelVersions` tabeli
- Možda treba više kvalitetnih gold labels
