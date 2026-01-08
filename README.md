# Content Moderation Agent

AI Agent za automatsku moderaciju sadržaja (komentari, postovi, poruke) sa kontekstualnom inteligencijom i kontinuiranim učenjem iz feedbacka.

## Arhitektura

Projekt koristi **Clean Architecture** sa sljedećim slojevima:

- **AiAgents.Core**: Framework abstrakcije (SoftwareAgent, IPerceptionSource, IPolicy, IActuator, ILearningComponent)
- **AiAgents.ContentModerationAgent**: Shared logika agenta
  - **Domain**: Entiteti i enum-i
  - **Application**: Servisi i Agent Runneri (Sense→Think→Act→Learn)
  - **Infrastructure**: DbContext, storage
  - **ML**: ML.NET klasifikacija
- **AiAgents.ContentModerationAgent.Web**: Web host/transport (Controllers, SignalR, Background Services)

## Agent Runneri

### 1. ModerationAgentRunner (Sense → Think → Act)
- **Sense**: Uzima sljedeći komentar iz queue-a
- **Think**: Klasificira koristeći ML model + kontekstualne faktore
- **Act**: Postavlja status (Allow/Review/Block) i upisuje prediction

### 2. RetrainAgentRunner (Sense → Think → Act → Learn)
- **Sense**: Provjerava broj novih gold labels
- **Think**: Odlučuje da li treba retrain
- **Act**: Trenira novi model
- **Learn**: Resetuje counter, ažurira settings

### 3. ThresholdUpdateRunner (Sense → Think → Act → Learn)
- **Sense**: Čita feedback metrike (FPR, FNR)
- **Think**: Odlučuje da li treba ažurirati pragove
- **Act**: Ažurira pragove
- **Learn**: Logira promjene

## Zahtjevi

- .NET 8.0 SDK
- SQL Server (LocalDB ili SQL Server Express)
- Visual Studio 2022 ili VS Code

## Pokretanje

1. **Restore paketa**:
   ```bash
   dotnet restore
   ```

2. **Build projekta**:
   ```bash
   dotnet build
   ```

3. **Pokreni aplikaciju**:
   ```bash
   cd src/AiAgents.ContentModerationAgent.Web
   dotnet run
   ```

4. **Otvori Swagger UI**:
   - Navigiraj na `https://localhost:5001/swagger` (ili port koji je dodijeljen)

## Brzo testiranje

### Opcija 1: Swagger UI (preporučeno)
1. Pokreni aplikaciju
2. Otvori `https://localhost:5001/swagger` u browseru
3. Koristi Swagger UI za testiranje API endpoints

### Opcija 2: PowerShell script
```powershell
.\test-api.ps1
```

### Opcija 3: Ručno kroz API
Pogledaj detaljne upute u `TESTING_GUIDE.md`

## API Endpoints

### Content
- `POST /api/content` - Kreira novi sadržaj (komentar/post/poruku)
- `GET /api/content/pending-review` - Vraća sve komentare koji čekaju review

### Review
- `POST /api/review/{contentId}/review` - Moderator daje feedback (gold label)

## SignalR Hub

- **Hub**: `/moderationHub`
- **Event**: `ModerationResult` - Emituje se kada agent procesira komentar

## Feedback Loop

Agent uči iz:
1. **Moderator feedback**: Gold labels iz review queue-a
2. **Korisnički reportovi**: Reportovani komentari (false negatives)
3. **Appeal proces**: Uspješni appealovi (false positives)
4. **Engagement metrike**: Blokirani komentari sa visokim engagementom

## Konfiguracija

Pragovi i postavke se mogu konfigurirati kroz `SystemSettings` tabelu:
- `AllowThreshold`: Prag za Allow (default: 0.3)
- `ReviewThreshold`: Prag za Review (default: 0.5)
- `BlockThreshold`: Prag za Block (default: 0.7)
- `RetrainThreshold`: Broj novih gold labels potrebnih za retraining (default: 100)

## Database

Aplikacija automatski kreira bazu pri prvom pokretanju (EnsureCreated).

Za migracije (kada budu potrebne):
```bash
dotnet ef migrations add InitialCreate --project src/AiAgents.ContentModerationAgent --startup-project src/AiAgents.ContentModerationAgent.Web
dotnet ef database update --project src/AiAgents.ContentModerationAgent --startup-project src/AiAgents.ContentModerationAgent.Web
```

## Struktura Projekta

```
AiAgents/
├── src/
│   ├── AiAgents.Core/
│   │   ├── SoftwareAgent.cs
│   │   ├── IPerceptionSource.cs
│   │   ├── IPolicy.cs
│   │   ├── IActuator.cs
│   │   └── ILearningComponent.cs
│   ├── AiAgents.ContentModerationAgent/
│   │   ├── Domain/
│   │   │   ├── Entities/
│   │   │   └── Enums/
│   │   ├── Application/
│   │   │   ├── Services/
│   │   │   ├── Runners/
│   │   │   └── DTOs/
│   │   ├── Infrastructure/
│   │   │   └── ContentModerationDbContext.cs
│   │   └── ML/
│   │       ├── IContentClassifier.cs
│   │       └── MlNetContentClassifier.cs
│   └── AiAgents.ContentModerationAgent.Web/
│       ├── Controllers/
│       ├── Hubs/
│       ├── BackgroundServices/
│       └── Program.cs
└── AiAgents.sln
```

## Napomene

- ML model je pojednostavljen za demonstraciju. U produkciji bi koristio naprednije feature engineering i multi-label klasifikaciju.
- Context calculation je također pojednostavljen. U produkciji bi koristio sentiment analysis i naprednije metrike.
- Background services rade kontinuirano i automatski procesiraju komentare.

## Razvoj

Za dalji razvoj:
1. Poboljšati ML model (feature engineering, multi-label classification)
2. Implementirati napredniji context calculation (sentiment analysis)
3. Dodati UI za moderatore (review queue dashboard)
4. Implementirati aktivno učenje
5. Dodati detaljnije metrike i monitoring
