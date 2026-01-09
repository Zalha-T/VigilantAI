# VigilantAI - Content Moderation Agent
## Comprehensive Documentation

---

## Table of Contents

1. [Introduction](#introduction)
2. [Project Overview](#project-overview)
3. [Agent Architecture](#agent-architecture)
4. [System Design](#system-design)
5. [Installation & Setup](#installation--setup)
6. [Usage Guide](#usage-guide)
7. [Technical Implementation](#technical-implementation)
8. [API Reference](#api-reference)
9. [Frontend Guide](#frontend-guide)
10. [Troubleshooting](#troubleshooting)
11. [Future Enhancements](#future-enhancements)

---

## Introduction

VigilantAI is an intelligent content moderation system that automatically analyzes and classifies user-generated content (comments, posts, messages) to determine if it should be allowed, sent for human review, or blocked. The system combines rule-based wordlist filtering with machine learning models that learn from moderator feedback, creating a continuously improving moderation system.

### What Makes VigilantAI an Agent?

VigilantAI is not just a classification tool—it is a **software agent** that:

- **Perceives** its environment (incoming content, user context, system state)
- **Thinks** and makes decisions (classifies content using ML models and rules)
- **Acts** on those decisions (updates content status, triggers reviews)
- **Learns** from feedback (retrains models based on moderator corrections)

The agent operates autonomously through continuous tick/step cycles, making decisions and adapting its behavior over time based on experience.

---

## Project Overview

### Problem Statement

Modern platforms face the challenge of moderating vast amounts of user-generated content. Manual moderation is time-consuming and doesn't scale. Automated systems need to:

- Accurately identify problematic content (spam, toxic language, hate speech, offensive material)
- Adapt to new patterns and evolving language
- Provide transparency in decision-making
- Allow human oversight for edge cases

### Solution: Multi-Agent System

VigilantAI implements a **multi-agent system** with specialized agents:

1. **Moderation Agent** (Classification Agent + Context-Aware Agent)
   - Classifies content in real-time
   - Considers author reputation, thread context, and engagement patterns
   - Makes Allow/Review/Block decisions

2. **Retraining Agent** (Learning Agent)
   - Monitors feedback accumulation
   - Triggers model retraining when sufficient new data is available
   - Manages model versioning and activation

3. **Threshold Update Agent** (Goal-Oriented Agent)
   - Monitors system performance metrics
   - Adjusts decision thresholds to optimize precision/recall balance

### Agent Type Justification

**Why Classification Agent?**
- Core function is making Allow/Review/Block decisions based on content analysis
- Uses thresholds and scoring to categorize content into zones

**Why Context-Aware Agent?**
- Same content may receive different decisions based on:
  - Author reputation and history
  - Time of day and engagement patterns
  - Thread context and sentiment

**Why Learning Agent?**
- System improves over time through retraining
- Adapts to new patterns and moderator feedback
- Maintains model version history for rollback capability

**Why Multi-Agent System?**
- Separation of concerns: each agent has a focused responsibility
- Scalability: agents can be scaled independently
- Maintainability: changes to one agent don't affect others

---

## Agent Architecture

### Sense → Think → Act → Learn Cycle

VigilantAI follows the standard agent cycle for each decision-making process:

#### 1. Moderation Agent Cycle

**Sense:**
- Retrieves next queued content from database (Status = Queued)
- Loads author information (reputation, account age, violation history)
- Retrieves thread context (if applicable)
- Checks for associated images

**Think:**
- Applies wordlist filtering (rule-based, instant blocking)
- Runs ML model prediction (text classification)
- Performs image classification (if image present)
- Calculates context multipliers (author reputation, time patterns)
- Combines scores using weighted formula
- Applies thresholds to determine decision

**Act:**
- Creates Prediction record with scores
- Updates Content status (Approved/PendingReview/Blocked)
- Sets ProcessedAt timestamp
- Emits SignalR event for real-time UI updates

**Learn:**
- Updates content metrics
- Logs decision for analysis
- (Indirect learning through Retraining Agent)

#### 2. Retraining Agent Cycle

**Sense:**
- Reads SystemSettings.NewGoldSinceLastTrain counter
- Checks RetrainThreshold setting
- Verifies RetrainingEnabled flag
- Counts available gold labels in database

**Think:**
- Compares NewGoldSinceLastTrain >= RetrainThreshold
- Validates sufficient training data (minimum 10 gold labels)
- Determines if retraining should occur

**Act:**
- Trains new ML model using gold labels
- Calculates model metrics (Accuracy, Precision, Recall, F1Score)
- Creates new ModelVersion record
- Activates new model (deactivates previous)
- Updates SystemSettings.LastRetrainDate

**Learn:**
- Resets NewGoldSinceLastTrain counter to 0
- Logs retraining event and metrics
- Stores model for future reference

#### 3. Threshold Update Agent Cycle

**Sense:**
- Reads feedback metrics (False Positive Rate, False Negative Rate)
- Checks current threshold settings
- Analyzes decision distribution

**Think:**
- Evaluates if thresholds need adjustment
- Calculates optimal threshold values
- Determines if change would improve performance

**Act:**
- Updates AllowThreshold, ReviewThreshold, BlockThreshold
- Logs threshold changes

**Learn:**
- Monitors impact of threshold changes
- Adjusts strategy based on results

### Agent Tick/Step Implementation

Each agent implements a `StepAsync()` method that represents one iteration of the Sense→Think→Act→Learn cycle:

```csharp
public async Task<ModerationTickResult?> StepAsync(CancellationToken cancellationToken)
{
    // SENSE: Get next queued content
    var content = await _queueService.GetNextQueuedContentAsync(cancellationToken);
    if (content == null) return null; // No work to do
    
    // THINK: Score and decide
    var prediction = await _scoringService.ScoreAndDecideAsync(content, cancellationToken);
    
    // ACT: Status already updated by ScoringService
    
    // Return result for host to emit
    return new ModerationTickResult { ... };
}
```

**Key Principles:**
- Each tick processes **one item** (atomic operation)
- Returns `null` when no work is available (no-work exit)
- All business logic is in shared layer, not Web layer
- Results are returned as DTOs for host to emit/log

---

## System Design

### Clean Architecture Layers

VigilantAI follows Clean Architecture principles with clear separation of concerns:

```
VigilantAI/
├── AiAgents.Core/              # Framework abstractions
│   ├── SoftwareAgent.cs        # Base agent class
│   ├── IPerceptionSource.cs    # Sense interface
│   ├── IPolicy.cs              # Think interface
│   ├── IActuator.cs            # Act interface
│   └── ILearningComponent.cs   # Learn interface
│
├── AiAgents.ContentModerationAgent/  # Shared agent logic
│   ├── Domain/                 # Entities and business rules
│   │   ├── Entities/           # Content, Author, Prediction, Review, etc.
│   │   └── Enums/              # ContentType, ContentStatus, ModerationDecision
│   │
│   ├── Application/            # Use cases and agent logic
│   │   ├── Services/           # Business logic services
│   │   │   ├── ScoringService.cs
│   │   │   ├── ReviewService.cs
│   │   │   ├── TrainingService.cs
│   │   │   └── WordlistService.cs
│   │   └── Runners/            # Agent tick implementations
│   │       ├── ModerationAgentRunner.cs
│   │       ├── RetrainAgentRunner.cs
│   │       └── ThresholdUpdateAgentRunner.cs
│   │
│   ├── Infrastructure/         # Data access and external services
│   │   ├── ContentModerationDbContext.cs
│   │   └── DatabaseSeeder.cs
│   │
│   └── ML/                     # Machine learning components
│       ├── IContentClassifier.cs
│       ├── MlNetContentClassifier.cs
│       ├── IImageClassifier.cs
│       └── ImageNetClassifier.cs
│
└── AiAgents.ContentModerationAgent.Web/  # Host/Transport layer
    ├── Controllers/             # API endpoints
    ├── Hubs/                   # SignalR real-time communication
    └── BackgroundServices/     # Agent host workers
```

### Layer Responsibilities

**Core Layer:**
- Generic agent abstractions
- No domain knowledge
- No EF, no ML.NET, no business logic

**Domain Layer:**
- Entities and value objects
- Business rules and invariants
- Domain events (if needed)
- No dependencies on other layers

**Application Layer:**
- Use case services (commands/queries)
- Agent runners (Sense→Think→Act→Learn)
- DTOs for data transfer
- Orchestrates domain and infrastructure

**Infrastructure Layer:**
- Database context and repositories
- File system operations
- External service integrations
- Implements application interfaces

**Web Layer (Host):**
- API controllers (thin wrappers)
- DTO mapping
- SignalR event emission
- Background service orchestration
- Dependency injection wiring
- **MUST NOT contain business logic**

### Key Architectural Rules

1. **Web layer is thin:** All business decisions (thresholds, status rules, retrain conditions) are in Application layer
2. **Runners contain agent logic:** Sense/Think/Act/Learn is clearly visible in runners
3. **Services are use cases:** Each service handles one business operation
4. **Domain is independent:** Domain entities don't know about infrastructure or web

---

## Installation & Setup

### Prerequisites

- **.NET 8.0 SDK** or later
- **SQL Server** (LocalDB, SQL Server Express, or full SQL Server)
- **Node.js 18+** and npm (for frontend)
- **Visual Studio 2022** or **VS Code** (recommended)

### Backend Setup

1. **Clone or navigate to the project:**
   ```bash
   cd backend
   ```

2. **Restore NuGet packages:**
   ```bash
   dotnet restore
   ```

3. **Update database connection string:**
   
   Edit `backend/src/AiAgents.ContentModerationAgent.Web/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ContentModerationDb;Trusted_Connection=true;TrustServerCertificate=true"
     }
   }
   ```

4. **Build the solution:**
   ```bash
   dotnet build backend/AiAgents.sln
   ```

5. **Run the application:**
   ```bash
   cd backend/src/AiAgents.ContentModerationAgent.Web
   dotnet run
   ```

   The API will be available at:
   - HTTPS: `https://localhost:60830`
   - HTTP: `http://localhost:60830`
   - Swagger UI: `https://localhost:60830/swagger`

### Frontend Setup

1. **Navigate to frontend directory:**
   ```bash
   cd frontend
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Configure API URL (if needed):**
   
   Create `.env` file or update `vite.config.ts`:
   ```env
   VITE_API_URL=https://localhost:60830/api
   VITE_HUB_URL=https://localhost:60830/moderationHub
   ```

4. **Run development server:**
   ```bash
   npm run dev
   ```

   Frontend will be available at `http://localhost:3000`

### Database Initialization

The database is automatically created on first run using Entity Framework's `EnsureCreated()` method. The system will:

1. Create all required tables
2. Seed initial system settings
3. Create default wordlist entries (if empty)

**Manual database reset:**
If you need to reset the database, delete the database file (for LocalDB) or drop the database, then restart the application.

### ONNX Model Setup

For image classification, download the ResNet50 ONNX model:

1. Download `resnet50-v2-7.onnx` from [ONNX Model Zoo](https://github.com/onnx/models/tree/main/vision/classification/resnet)
2. Place it in: `backend/src/AiAgents.ContentModerationAgent.Web/models/resnet50-v2-7.onnx`

The system will automatically detect and load the model on startup.

**Image:** [Screenshot showing models folder structure with resnet50-v2-7.onnx file]

---

## Usage Guide

### Dashboard Overview

The main dashboard provides an overview of all content in the system.

**Image:** [Screenshot of dashboard showing content cards with status badges, search bar, and filter dropdown]

**Features:**
- **Status Filter:** Filter content by status (All, Queued, Processing, Approved, Pending Review, Blocked)
- **Search:** Search by content text or author username (press Enter to search)
- **Content Cards:** Each card shows:
  - Status badge (color-coded)
  - Content type
  - Content text (truncated)
  - Author information
  - Prediction scores and labels
  - Delete button (bottom right, appears on hover)

### Creating Content

1. Navigate to **Create Content** from the navbar
2. Fill in the form:
   - **Content Type:** Select Comment (1), Post (2), or Message (3)
   - **Text:** Enter the content text
   - **Author Username:** Enter author username (creates author if doesn't exist)
   - **Thread ID:** (Optional) For threaded content
   - **Image:** (Only for Posts) Upload an image file
3. Click **Submit**

**Image:** [Screenshot of Create Content form with all fields visible]

The content will be automatically processed by the Moderation Agent and appear on the dashboard.

### Review Queue

The Review Queue shows content that requires human moderator review.

**Image:** [Screenshot of Review Queue page showing pending content with Allow/Review/Block buttons]

**Actions:**
- **Allow:** Content is approved, agent decision was correct
- **Review:** Content needs further review, agent decision was partially correct
- **Block:** Content is blocked, agent decision was correct

Each action:
- Creates a Review record with gold label
- Updates content status
- Increments NewGoldSinceLastTrain counter
- May trigger immediate retraining if threshold is reached

### Content Details

Click any content card to view detailed information.

**Image:** [Screenshot of Content Details page showing full content, prediction scores, image classification, and review information]

**Information displayed:**
- Full content text
- Author details and reputation
- Agent prediction with individual scores (Spam, Toxic, Hate, Offensive)
- Final score and decision
- Image classification (if image present)
- Review history (if reviewed)
- Context factors (author reputation, time of day, etc.)

**Actions:**
- **Send to Review Queue:** Manually send content for review
- **Delete Content:** Remove content from system

### Settings

Configure system thresholds and retraining parameters.

**Image:** [Screenshot of Settings page showing threshold inputs and system status]

**Moderation Thresholds:**
- **Allow Threshold:** Content below this score is automatically approved (default: 0.3)
- **Review Threshold:** Content between Allow and Review thresholds goes to review queue (default: 0.5)
- **Block Threshold:** Content above this score is automatically blocked (default: 0.7)

**Retraining Settings:**
- **Retrain Threshold:** Number of new gold labels needed to trigger retraining (default: 6)
- **Retraining Enabled:** Toggle to enable/disable automatic retraining

**Current Status:**
- New Gold Labels Since Last Train
- Last Retrain Date
- Retraining status and warnings

### Wordlist Management

Manage the wordlist used for instant content filtering.

**Image:** [Screenshot of Wordlist Settings page showing table of words with categories and actions]

**Categories:**
- **Toxic:** Offensive, toxic words
- **Hate:** Hate speech indicators
- **Spam:** Spam phrases
- **Offensive:** Offensive language
- **Slur:** Specific slurs (manually added)

**Actions:**
- **Add Word:** Add new word to wordlist
- **Edit:** Modify word or category
- **Activate/Deactivate:** Toggle word without deleting
- **Delete:** Remove word permanently

**Note:** Words are matched using word boundary detection to avoid false positives (e.g., "guys" won't match "hello guys" unless "guys" is a complete word).

---

## Technical Implementation

### Scoring Algorithm

Content scoring combines multiple factors:

```
Final Score = (SpamScore × 0.3 + ToxicScore × 0.3 + HateScore × 0.25 + OffensiveScore × 0.15) × ContextMultiplier
```

**Score Components:**
- **Spam Score:** Based on spam keywords, repeated words, excessive punctuation, ALL CAPS
- **Toxic Score:** Based on toxic keywords and wordlist matches
- **Hate Score:** Based on hate speech indicators
- **Offensive Score:** Based on offensive language patterns

**Context Multiplier:**
- Author reputation (0-1, based on reputation score, account age, violations)
- Thread sentiment (placeholder for future implementation)
- Engagement level (placeholder for future implementation)
- Time of day patterns (placeholder for future implementation)

**Decision Logic:**
```
if FinalScore < AllowThreshold:
    Decision = Allow, Status = Approved
elif FinalScore > BlockThreshold:
    Decision = Block, Status = Blocked
else:
    Decision = Review, Status = PendingReview
```

### Wordlist vs ML Model

**Wordlist (Rule-Based):**
- **Purpose:** Instant blocking of explicit words/phrases
- **How it works:** Exact word boundary matching against blocked words
- **Advantages:** Fast, explicit, no false negatives for known bad words
- **When to use:** Add words that should always be blocked immediately

**ML Model (Learning-Based):**
- **Purpose:** Contextual understanding and pattern recognition
- **How it works:** Trained on gold labels, learns patterns in text
- **Advantages:** Adapts to new patterns, understands context
- **When to use:** Improves over time with more feedback

**Combined Approach:**
1. Wordlist checks first (instant blocking)
2. ML model analyzes full text context
3. Scores are combined for final decision

### Image Classification

When content includes an image:

1. **Image is classified** using ResNet50 ONNX model (ImageNet classes)
2. **Top prediction** is extracted (e.g., "pistol", "knife", "dog")
3. **If confidence > 30%:** Image label is appended to text for wordlist checking
4. **Special handling:** Dog images boost toxic/hate/offensive scores (example rule)

**Image:** [Diagram showing image classification flow: Upload → ONNX Model → ImageNet Labels → Wordlist Check → Score Boost]

### Retraining Process

**Trigger Conditions:**
- NewGoldSinceLastTrain >= RetrainThreshold
- RetrainingEnabled = true
- At least 10 gold labels available in database

**Process:**
1. Load all gold labels (Reviews with GoldLabel)
2. Prepare training data (text → labels)
3. Train ML.NET FastTree model
4. Calculate metrics (Accuracy, Precision, Recall, F1Score)
5. Create new ModelVersion record
6. Activate new model (deactivate previous)
7. Update SystemSettings (LastRetrainDate, reset counter)

**Model Versioning:**
- Each retraining creates a new version
- Previous versions are kept for rollback
- Only one model is active at a time

**Image:** [Flowchart showing retraining process: Check Threshold → Load Gold Labels → Train Model → Calculate Metrics → Create Version → Activate]

### Background Services

Three background services run continuously:

1. **ModerationAgentBackgroundService**
   - Runs ModerationAgentRunner every 2 seconds
   - Processes queued content
   - Emits SignalR events on completion

2. **RetrainAgentBackgroundService**
   - Runs RetrainAgentRunner every 5 minutes
   - Checks retraining threshold
   - Triggers retraining if needed

3. **ThresholdUpdateAgentBackgroundService**
   - Runs ThresholdUpdateAgentRunner every 10 minutes
   - Monitors performance metrics
   - Adjusts thresholds if needed

**Service Lifecycle:**
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        using var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ModerationAgentRunner>();
        
        var result = await runner.StepAsync(stoppingToken);
        if (result != null)
        {
            await _hubContext.Clients.All.SendAsync("ModerationResult", result);
        }
        
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
    }
}
```

---

## API Reference

### Content Endpoints

#### Create Content
```
POST /api/content
Content-Type: multipart/form-data (with image) or application/json (without image)
```

**Request Body (JSON):**
```json
{
  "type": 2,
  "text": "Hello world",
  "authorUsername": "user123",
  "threadId": "optional-guid"
}
```

**Request Body (Form Data):**
- `type`: ContentType (1=Comment, 2=Post, 3=Message)
- `text`: Content text
- `authorUsername`: Author username
- `threadId`: (Optional) Thread ID
- `image`: (Optional, only for Posts) Image file

**Response:**
```json
{
  "id": "guid",
  "type": 2,
  "text": "Hello world",
  "status": 3,
  "author": { ... },
  "createdAt": "2026-01-09T10:00:00Z"
}
```

#### Get All Content
```
GET /api/content?status=4&searchText=hello&page=1&pageSize=50
```

**Query Parameters:**
- `status`: (Optional) Filter by ContentStatus
- `searchText`: (Optional) Search in text and author username
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 50)

**Response:**
```json
{
  "data": [ ... ],
  "totalPages": 5,
  "currentPage": 1,
  "pageSize": 50
}
```

#### Get Content by ID
```
GET /api/content/{id}
```

**Response:**
```json
{
  "id": "guid",
  "text": "...",
  "status": 4,
  "prediction": {
    "spamScore": 0.05,
    "toxicScore": 0.05,
    "hateScore": 0.05,
    "offensiveScore": 0.70,
    "finalScore": 0.162,
    "decision": 1
  },
  "author": { ... },
  "image": { ... }
}
```

#### Delete Content
```
DELETE /api/content/{id}
```

**Response:** 204 No Content

#### Send to Review Queue
```
POST /api/content/{id}/send-to-review
```

**Response:** 200 OK

### Review Endpoints

#### Submit Review
```
POST /api/review/{contentId}/review
```

**Request Body:**
```json
{
  "goldLabel": 1,
  "correctDecision": true,
  "feedback": "Optional feedback text",
  "moderatorId": null
}
```

**Gold Label Values:**
- 1 = Allow
- 2 = Review
- 3 = Block

**Response:**
```json
{
  "id": "guid",
  "contentId": "guid",
  "goldLabel": 1,
  "correctDecision": true,
  "createdAt": "2026-01-09T10:00:00Z"
}
```

#### Get Pending Reviews
```
GET /api/review/pending
```

**Response:**
```json
[
  {
    "id": "guid",
    "text": "...",
    "author": { ... },
    "prediction": { ... }
  }
]
```

### Settings Endpoints

#### Get Settings
```
GET /api/settings
```

#### Update Thresholds
```
PUT /api/settings/thresholds
```

**Request Body:**
```json
{
  "allowThreshold": 0.3,
  "reviewThreshold": 0.5,
  "blockThreshold": 0.7
}
```

#### Update Retrain Threshold
```
PUT /api/settings/retrain-threshold
```

**Request Body:**
```json
{
  "retrainThreshold": 6
}
```

### Wordlist Endpoints

#### Get All Words
```
GET /api/wordlist
```

#### Get Words by Category
```
GET /api/wordlist/category/{category}
```

#### Add Word
```
POST /api/wordlist
```

**Request Body:**
```json
{
  "word": "example",
  "category": "toxic"
}
```

#### Update Word
```
PUT /api/wordlist/{id}
```

**Request Body:**
```json
{
  "word": "updated",
  "category": "hate",
  "isActive": true
}
```

#### Delete Word
```
DELETE /api/wordlist/{id}
```

### SignalR Hub

**Hub Path:** `/moderationHub`

**Events:**
- `ModerationResult`: Emitted when content is processed
  ```json
  {
    "contentId": "guid",
    "decision": 1,
    "finalScore": 0.162,
    "status": 3
  }
  ```

---

## Frontend Guide

### Architecture

The frontend is built with:
- **React 18** with TypeScript
- **React Router** for navigation
- **Vite** as build tool
- **Axios** for API calls
- **SignalR** for real-time updates

### Component Structure

```
frontend/src/
├── pages/
│   ├── Dashboard.tsx          # Main content overview
│   ├── ReviewQueue.tsx         # Pending reviews
│   ├── ContentDetails.tsx      # Content detail view
│   ├── CreateContent.tsx       # Content creation form
│   ├── Settings.tsx            # System settings
│   └── WordlistSettings.tsx    # Wordlist management
│
├── components/
│   ├── LoadingSpinner.tsx      # Loading animation
│   ├── SkeletonCard.tsx        # Loading skeleton
│   ├── Toast.tsx               # Toast notifications
│   ├── ToastContainer.tsx      # Toast manager
│   └── ConfirmDialog.tsx      # Confirmation dialogs
│
└── services/
    ├── api.ts                  # API client
    └── signalr.ts              # SignalR connection
```

### State Management

The frontend uses React hooks for state management:
- `useState` for local component state
- `useEffect` for side effects and data loading
- Custom hooks for SignalR connection

### Real-Time Updates

SignalR connection is established on app startup and listens for `ModerationResult` events. When content is processed, the dashboard automatically updates.

**Image:** [Sequence diagram showing: User Action → API Call → Backend Processing → SignalR Event → Frontend Update]

### UI Features

**Modern Design:**
- Gradient buttons with hover effects
- Card hover animations (scale and shadow)
- Status badges with color coding
- Smooth page transitions
- Loading skeletons during data fetch

**User Experience:**
- Toast notifications for all actions
- Confirmation dialogs for destructive actions
- Search with Enter key trigger
- Pagination for large datasets
- Responsive design

---

## Troubleshooting

### Common Issues

#### Database Connection Errors

**Problem:** Cannot connect to database

**Solutions:**
1. Verify SQL Server is running
2. Check connection string in `appsettings.json`
3. For LocalDB, ensure it's installed and running
4. Check firewall settings

#### Model File Not Found

**Problem:** Image classification not working, model file error

**Solutions:**
1. Verify `resnet50-v2-7.onnx` exists in `models/` folder
2. Check file permissions
3. Verify model path in logs

#### Retraining Not Triggering

**Problem:** Retraining doesn't occur despite reaching threshold

**Solutions:**
1. Check `RetrainingEnabled` setting
2. Verify at least 10 gold labels exist
3. Check background service is running
4. Review logs for errors

#### Wordlist Not Working

**Problem:** Words in wordlist not being detected

**Solutions:**
1. Verify word is active (`IsActive = true`)
2. Check word is in correct category
3. Verify word boundary matching (whole words only)
4. Check word is lowercase in database

#### Frontend Not Connecting to Backend

**Problem:** API calls fail, CORS errors

**Solutions:**
1. Verify backend is running
2. Check API URL in frontend config
3. Verify CORS is enabled in backend
4. Check browser console for errors

### Debugging Tips

1. **Check Backend Logs:**
   - Look for agent tick results
   - Monitor retraining events
   - Check for exceptions

2. **Use Swagger UI:**
   - Test API endpoints directly
   - Verify request/response formats

3. **Browser DevTools:**
   - Check Network tab for API calls
   - Monitor SignalR connection
   - Review console for errors

4. **Database Inspection:**
   - Check Content table for status updates
   - Verify Reviews table for gold labels
   - Monitor SystemSettings for threshold changes

---

## Future Enhancements

### Planned Features

1. **Active Learning**
   - Agent identifies uncertain cases
   - Requests human review for ambiguous content
   - Prioritizes learning from edge cases

2. **Adaptive Thresholds**
   - Automatic threshold adjustment based on performance
   - A/B testing of threshold values
   - Performance-based optimization

3. **Explanation System**
   - "Why did the agent make this decision?"
   - Highlights contributing factors
   - Shows which words/patterns triggered scores

4. **Multi-Agent Coordination**
   - Specialized agents for different content types
   - Agent communication and consensus
   - Load balancing across agents

5. **Sentiment Analysis**
   - Thread sentiment calculation
   - Context-aware decision making
   - Engagement pattern analysis

6. **Advanced Image Analysis**
   - Object detection (not just classification)
   - Text extraction from images (OCR)
   - NSFW detection

7. **Simulation Environment**
   - Test agent behavior before deployment
   - Simulate different scenarios
   - Performance testing

### Extension Ideas

- **Appeal Process:** Users can appeal blocked content
- **Bulk Operations:** Process multiple items at once
- **Export/Import:** Backup and restore wordlists
- **Analytics Dashboard:** Visualize agent performance
- **Custom Models:** Train domain-specific models
- **Multi-Language Support:** Detect and handle multiple languages

---

## Conclusion

VigilantAI demonstrates a complete software agent system that:

- Perceives its environment (content, context, feedback)
- Thinks and makes decisions (classification, scoring, thresholds)
- Acts on those decisions (status updates, reviews, retraining)
- Learns from experience (model retraining, threshold adjustment)

The system combines rule-based filtering with machine learning, creating a robust and adaptable content moderation solution that improves over time through human feedback.

---

**Document Version:** 1.0  
**Last Updated:** January 2026  
**Project:** VigilantAI - Content Moderation Agent
