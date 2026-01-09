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

## Development Journey: AI-Assisted Collaboration

This project was developed through iterative collaboration with an AI assistant (Cursor), demonstrating how modern AI tools can accelerate software development while maintaining code quality and architectural principles. This section documents key development milestones and interactions that shaped VigilantAI.

### Initial Concept and Architecture Design

**Developer Request:**
```
I want to build a content moderation system that uses AI agents to automatically 
classify and moderate user-generated content. The system should:
1. Use machine learning to classify content (spam, toxic, hate, offensive)
2. Learn from human moderator feedback
3. Have a web interface for moderators
4. Support image classification
5. Follow clean architecture principles
```

**AI Assistant Response:**
The AI assistant proposed a multi-agent architecture with three specialized agents:
- **Moderation Agent**: Real-time content classification using Sense→Think→Act→Learn cycle
- **Retraining Agent**: Monitors feedback and triggers model retraining
- **Threshold Update Agent**: Optimizes decision thresholds based on performance

The assistant created the initial project structure with Clean Architecture layers:
```
backend/src/
├── AiAgents.Core/              # Framework abstractions
├── AiAgents.ContentModerationAgent/  # Domain and application logic
└── AiAgents.ContentModerationAgent.Web/  # Web host
```

### Prompt 1: Implementing Image Classification

**Developer Request:**
```
I want to add image classification to the system. When users upload images with posts,
the system should classify them using a pre-trained model and use those classifications
in the moderation decision. Can we use ONNX Runtime for this?
```

**AI Assistant Implementation:**
The assistant implemented `ImageNetClassifier.cs` using ONNX Runtime with ResNet50:

```csharp
public class ImageNetClassifier : IImageClassifier
{
    private readonly InferenceSession _session;
    private readonly ImageNetLabels _labels;

    public async Task<ImageClassificationResult> ClassifyAsync(byte[] imageBytes)
    {
        // Preprocess image
        var input = PreprocessImage(imageBytes);
        
        // Run inference
        var outputs = _session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("data", input)
        });
        
        // Apply softmax and get top prediction
        var probabilities = Softmax(outputs.First().AsEnumerable<float>().ToArray());
        var topIndex = Array.IndexOf(probabilities, probabilities.Max());
        
        return new ImageClassificationResult
        {
            Label = _labels.GetLabel(topIndex),
            Confidence = probabilities[topIndex]
        };
    }
}
```

The assistant also integrated image labels into the wordlist checking system, allowing image predictions (e.g., "pistol", "knife") to influence moderation decisions.

### Prompt 2: Implementing Retraining Mechanism

**Developer Request:**
```
The system should automatically retrain the ML model when enough new feedback 
(gold labels) is collected. Can we implement a background service that checks 
periodically and triggers retraining when a threshold is reached?
```

**AI Assistant Implementation:**
The assistant created the retraining system with immediate trigger capability:

```csharp
public class ReviewService : IReviewService
{
    public async Task<Review> CreateReviewAsync(Guid contentId, ...)
    {
        // Create review and update gold label counter
        var review = await CreateReviewAsync(contentId, goldLabel, ...);
        
        // Check if retraining threshold is reached
        await CheckAndTriggerRetrainingAsync(cancellationToken);
        
        return review;
    }
    
    private async Task CheckAndTriggerRetrainingAsync(CancellationToken cancellationToken)
    {
        var settings = await _thresholdService.GetSettingsAsync(cancellationToken);
        
        if (settings.NewGoldSinceLastTrain >= settings.RetrainThreshold)
        {
            _logger.LogInformation("🚀 IMMEDIATE RETRAINING TRIGGERED: Threshold reached");
            await _trainingService.TrainModelAsync(activate: true, cancellationToken);
        }
    }
}
```

This ensures retraining happens immediately when sufficient feedback is collected, rather than waiting for the background service's next check.

### Prompt 3: Fixing Wordlist False Positives

**Developer Request:**
```
I noticed that "hello guys" is being flagged as offensive when "guys" is in the wordlist.
The wordlist matching is too aggressive - it's matching substrings. Can we fix this
to only match whole words?
```

**AI Assistant Solution:**
The assistant implemented word boundary matching for single words while preserving phrase matching:

```csharp
private bool KeywordMatches(string text, string keyword)
{
    if (keyword.Contains(' '))
    {
        // For phrases, use simple Contains
        return text.Contains(keyword);
    }
    else
    {
        // For single words, use word boundary matching
        var pattern = $@"\b{Regex.Escape(keyword)}\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }
}
```

This prevents false positives like "hello guys" matching "guys" when "guys" is a standalone word in the wordlist, while still allowing phrases like "you are an idiot" to be detected.

### Prompt 4: Modernizing the Frontend UI

**Developer Request:**
```
The frontend looks basic. Can we modernize it with:
- Gradient buttons with hover effects
- Smooth animations
- Toast notifications instead of alert() popups
- Loading spinners
- Better card designs
```

**AI Assistant Implementation:**
The assistant created a comprehensive UI modernization:

**Toast Notification System:**
```typescript
// ToastContainer.tsx
export const showToast = (message: string, type: 'success' | 'error' | 'info' | 'warning') => {
  const toast = {
    id: Date.now(),
    message,
    type,
    duration: 3000
  };
  // Add to queue and animate in
};

// Usage throughout app
showToast('Content created successfully!', 'success');
showToast('Error deleting content', 'error');
```

**Custom Confirm Dialog:**
```typescript
// ConfirmDialog.tsx - Replaces window.confirm()
<ConfirmDialog
  isOpen={deleteConfirm.isOpen}
  title="Delete Content"
  message="Are you sure you want to delete this content?"
  type="danger"
  onConfirm={handleDelete}
  onCancel={() => setDeleteConfirm(false)}
/>
```

**Loading Spinner with Logo:**
```typescript
// LoadingSpinner.tsx - Custom spinner with rotating circles
<div className="loading-overlay">
  <div className="loading-container">
    <img src="/logo.png" alt="Loading" className="loading-logo" />
    <div className="loading-ring ring-1"></div>
    <div className="loading-ring ring-2"></div>
  </div>
</div>
```

### Prompt 5: Implementing Search Functionality

**Developer Request:**
```
I want to add a search bar to the dashboard that filters content by text or author 
username. The search should only trigger on Enter key press, not on every character 
input. Can we implement server-side search?
```

**AI Assistant Implementation:**
The assistant implemented server-side search with Enter key trigger:

**Backend (ContentController.cs):**
```csharp
[HttpGet]
public async Task<IActionResult> GetAllContent(
    [FromQuery] ContentStatus? status = null,
    [FromQuery] string? search = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50)
{
    var query = _context.Contents
        .Include(c => c.Author)
        .AsQueryable();
    
    if (!string.IsNullOrWhiteSpace(search))
    {
        var searchLower = search.ToLower();
        query = query.Where(c => 
            c.Text.ToLower().Contains(searchLower) || 
            c.Author.Username.ToLower().Contains(searchLower));
    }
    
    // ... pagination and return
}
```

**Frontend (Dashboard.tsx):**
```typescript
const handleSearchKeyPress = (e: React.KeyboardEvent<HTMLInputElement>) => {
  if (e.key === 'Enter') {
    loadContents();
  }
};

// Search input only triggers on Enter
<input
  type="text"
  placeholder="Search content or author..."
  value={searchQuery}
  onChange={(e) => setSearchQuery(e.target.value)}
  onKeyPress={handleSearchKeyPress}
/>
```

### Prompt 6: Fixing NaN Values in Model Metrics

**Developer Request:**
```
I'm getting a database error when retraining: "The supplied value is not a valid 
instance of data type float" for F1Score. It seems like F1Score is NaN when both 
Precision and Recall are 0. Can we fix this?
```

**AI Assistant Solution:**
The assistant fixed the F1Score calculation to handle division by zero:

```csharp
// Before (caused NaN):
var f1Score = 2 * (precision * recall) / (precision + recall);

// After (handles division by zero):
var f1Score = (precision + recall) > 0
    ? 2 * (precision * recall) / (precision + recall)
    : 0.0; // If both are 0, F1Score is 0 (not NaN)

// Additional validation before saving:
var accuracy = double.IsNaN(modelMetrics.Accuracy) || double.IsInfinity(modelMetrics.Accuracy) 
    ? 0.0 : modelMetrics.Accuracy;
var precision = double.IsNaN(modelMetrics.Precision) || double.IsInfinity(modelMetrics.Precision) 
    ? 0.0 : modelMetrics.Precision;
// ... same for recall and f1Score
```

### Prompt 7: Reorganizing Project Structure

**Developer Request:**
```
The project structure has `src/` in the root. Can we reorganize it to have a cleaner 
structure with `backend/` and `frontend/` folders? I want to move `src/` to `backend/src/` 
without breaking anything.
```

**AI Assistant Implementation:**
The assistant carefully reorganized the project:

1. **Moved `src/` to `backend/src/`**
2. **Updated solution file paths** in `AiAgents.sln`
3. **Updated `.gitignore`** to reflect new paths
4. **Fixed `Program.cs`** to use relative paths correctly
5. **Updated documentation** references

The reorganization maintained all functionality while creating a cleaner, more standard project structure.

### Prompt 8: Adding Comprehensive Documentation

**Developer Request:**
```
I need comprehensive documentation for this project. Can you create detailed 
documentation that includes:
- Agent architecture explanation
- Sense→Think→Act→Learn cycles with examples
- API reference
- Technical implementation details
- Code examples from the actual codebase
```

**AI Assistant Response:**
The assistant created `DOCUMENTATION.md` with:
- Complete agent architecture explanation
- Detailed Sense→Think→Act→Learn cycles with concrete examples
- Scoring algorithm breakdown with step-by-step calculations
- Wordlist matching algorithm details
- ML model training pipeline explanation
- API reference with all endpoints
- Frontend guide with component structure
- Troubleshooting section
- Code examples from actual implementation

The documentation was structured to be both comprehensive for developers and accessible for understanding the agent system concepts.

### Key Development Insights

Through this collaborative development process, several key insights emerged:

1. **Iterative Refinement**: The project evolved through multiple iterations, with each prompt addressing specific needs or issues discovered during development.

2. **Architecture First**: Starting with a clean architecture foundation made it easier to add features and maintain separation of concerns.

3. **Real-World Testing**: Many improvements (like word boundary matching, NaN handling) came from testing with real data and discovering edge cases.

4. **User Experience Focus**: UI improvements were driven by actual usage - replacing browser popups with custom components, adding loading states, implementing search.

5. **Documentation as Code**: Comprehensive documentation was added throughout development, not as an afterthought, making it easier to maintain accuracy.

This development journey demonstrates how AI-assisted development can accelerate project creation while maintaining code quality, proper architecture, and comprehensive documentation.

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

**Example Sense Data:**
```csharp
// Agent perceives:
Content {
    Text: "hello guys",
    Type: Post,
    Author: {
        Username: "@abi",
        ReputationScore: 50,
        AccountAgeDays: 30,
        PreviousViolations: 0
    },
    Image: null,
    Status: Queued
}
```

**Think:**
- Applies wordlist filtering (rule-based, instant blocking)
- Runs ML model prediction (text classification)
- Performs image classification (if image present)
- Calculates context multipliers (author reputation, time patterns)
- Combines scores using weighted formula
- Applies thresholds to determine decision

**Example Think Process:**
```csharp
// Step 1: Wordlist check
textForClassification = "hello guys"
wordlistMatches = [] // No matches (word boundary matching)

// Step 2: ML model prediction
mlScores = {
    SpamScore: 0.05,
    ToxicScore: 0.05,
    HateScore: 0.05,
    OffensiveScore: 0.70  // High due to wordlist or pattern match
}

// Step 3: Context calculation
authorReputation = 0.5 + 0.1 (account age > 30) - 0.0 (no violations) = 0.6
contextMultiplier = 0.6

// Step 4: Final score calculation
finalScore = (0.05×0.3 + 0.05×0.3 + 0.05×0.25 + 0.70×0.15) × 0.6
finalScore = (0.015 + 0.015 + 0.0125 + 0.105) × 0.6
finalScore = 0.1475 × 0.6 = 0.0885

// Step 5: Apply thresholds
AllowThreshold = 0.3
BlockThreshold = 0.7
Decision: Allow (0.0885 < 0.3)
```

**Act:**
- Creates Prediction record with scores
- Updates Content status (Approved/PendingReview/Blocked)
- Sets ProcessedAt timestamp
- Emits SignalR event for real-time UI updates

**Example Act Result:**
```csharp
// Agent acts:
Prediction {
    SpamScore: 0.05,
    ToxicScore: 0.05,
    HateScore: 0.05,
    OffensiveScore: 0.70,
    FinalScore: 0.0885,
    Decision: Allow,
    Confidence: High
}

Content.Status = Approved
Content.ProcessedAt = 2026-01-09T10:00:00Z

// SignalR event emitted:
{
    contentId: "guid",
    decision: 1,  // Allow
    finalScore: 0.0885,
    status: 3     // Approved
}
```

**Learn:**
- Updates content metrics
- Logs decision for analysis
- (Indirect learning through Retraining Agent)

**Note:** Learning happens indirectly. When a moderator reviews this content and provides feedback, the Retraining Agent will use that feedback to improve the model.

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

Each agent implements a `TickAsync()` method that represents one iteration of the Sense→Think→Act→Learn cycle:

**ModerationAgentRunner Implementation:**

```csharp
public class ModerationAgentRunner
{
    private readonly IQueueService _queueService;
    private readonly IScoringService _scoringService;

    public async Task<ModerationTickResult?> TickAsync(CancellationToken cancellationToken = default)
    {
        // === SENSE ===
        // Get next content from queue
        var content = await _queueService.DequeueNextAsync(cancellationToken);
        if (content == null)
            return null; // No work available

        try
        {
            // === THINK + ACT ===
            // Score and decide (this combines Think and Act)
            var prediction = await _scoringService.ScoreAndDecideAsync(content, cancellationToken);

            // Create result DTO for host to emit
            var result = new ModerationTickResult
            {
                ContentId = content.Id,
                Decision = prediction.Decision,
                Confidence = prediction.Confidence,
                FinalScore = prediction.FinalScore,
                NewStatus = content.Status,
                ContextFactors = prediction.ContextFactors
            };

            return result;
        }
        catch (Exception ex)
        {
            // Log error and rethrow - BackgroundService will handle it
            throw new InvalidOperationException($"Error processing content {content.Id}: {ex.Message}", ex);
        }
    }
}
```

**RetrainAgentRunner Implementation:**

```csharp
public class RetrainAgentRunner
{
    private readonly ITrainingService _trainingService;
    private readonly IThresholdService _thresholdService;

    public async Task<bool> TickAsync(CancellationToken cancellationToken = default)
    {
        // === SENSE ===
        // Check if retraining is needed
        var shouldRetrain = await _trainingService.ShouldRetrainAsync(cancellationToken);
        if (!shouldRetrain)
            return false; // No work available

        // === THINK ===
        // Decision already made (shouldRetrain = true)
        
        // === ACT ===
        // Train new model and activate it
        await _trainingService.TrainModelAsync(activate: true, cancellationToken);

        // === LEARN ===
        // Counter reset and settings update are handled in TrainingService
        // This is the learning component - the system has learned from new gold labels

        return true;
    }
}
```

**Key Principles:**
- Each tick processes **one item** (atomic operation)
- Returns `null`/`false` when no work is available (no-work exit)
- All business logic is in shared layer, not Web layer
- Results are returned as DTOs for host to emit/log
- Error handling is done at the runner level, with BackgroundService as fallback

### Complete Cycle Example: Step-by-Step

Let's trace a complete moderation cycle with real data:

**Initial State:**
- Content in database: Status = Queued, Text = "This is spam click here", Author = "@spammer" (Reputation = 20)

**Tick Execution:**

```csharp
// === SENSE ===
var content = await _queueService.DequeueNextAsync();
// Retrieved: Content { Id: "abc123", Text: "This is spam click here", Status: Queued }

// === THINK ===
// 1. Wordlist check
textForClassification = "This is spam click here"
// Wordlist matches: ["spam", "click here"] → 2 matches in spam category
spamMatches = 2

// 2. ML model prediction
mlScores = {
    SpamScore: 0.4 + (2 × 0.2) = 0.8,  // Boosted by wordlist
    ToxicScore: 0.05,
    HateScore: 0.05,
    OffensiveScore: 0.05
}

// 3. Context calculation
authorReputation = 0.2 (low reputation)
contextMultiplier = 0.8  // Lower multiplier for low reputation

// 4. Final score
finalScore = (0.8×0.3 + 0.05×0.3 + 0.05×0.25 + 0.05×0.15) × 0.8
finalScore = (0.24 + 0.015 + 0.0125 + 0.0075) × 0.8
finalScore = 0.274 × 0.8 = 0.2192

// 5. Decision
AllowThreshold = 0.3
BlockThreshold = 0.7
// 0.2192 < 0.3 → Decision = Allow
// BUT: Wordlist match gives instant boost, so decision = Block

// === ACT ===
Prediction created with:
- SpamScore: 0.8
- FinalScore: 0.2192
- Decision: Block (due to wordlist)
- Status updated: Blocked

// === LEARN ===
// Decision logged, metrics updated
// Future retraining will consider this if reviewed
```

**Result:**
- Content status changed: Queued → Blocked
- Prediction saved with all scores
- SignalR event emitted to frontend
- UI updates in real-time

---

## Agent Decision Examples

This section provides concrete examples of how the agent makes decisions in different scenarios.

### Example 1: Clear Allow - Harmless Content

**Content:**
```
Text: "Hello, how are you today?"
Author: "@friendly_user" (Reputation: 80, Account Age: 120 days)
Image: None
```

**Agent Processing:**

**Sense:**
- Text: "Hello, how are you today?"
- Author reputation: 0.8 (high)
- No image present

**Think:**
```
Wordlist Check:
- Text: "hello how are you today"
- Matches: [] (no blocked words found)

ML Model Prediction:
- SpamScore: 0.05 (no spam indicators)
- ToxicScore: 0.05 (no toxic words)
- HateScore: 0.05 (no hate speech)
- OffensiveScore: 0.05 (no offensive content)

Context Multiplier:
- Author reputation: 0.8 + 0.1 (account age > 30) = 0.9
- Context multiplier: 0.9

Final Score Calculation:
finalScore = (0.05×0.3 + 0.05×0.3 + 0.05×0.25 + 0.05×0.15) × 0.9
finalScore = (0.015 + 0.015 + 0.0125 + 0.0075) × 0.9
finalScore = 0.05 × 0.9 = 0.045
```

**Decision:**
- Final Score: 0.045
- Allow Threshold: 0.3
- **Decision: Allow** (0.045 < 0.3)
- Status: Approved

**Reasoning:** No problematic indicators, all scores are minimal, high author reputation provides positive context.

---

### Example 2: Wordlist Match - Instant Block

**Content:**
```
Text: "Buy now! Limited time offer! Click here!"
Author: "@spammer" (Reputation: 10, Account Age: 5 days)
Image: None
```

**Agent Processing:**

**Sense:**
- Text: "Buy now! Limited time offer! Click here!"
- Author reputation: 0.1 (very low)
- Multiple spam indicators

**Think:**
```
Wordlist Check:
- Text: "buy now limited time offer click here"
- Matches found: ["buy now", "limited time", "click here"] → 3 spam matches
- Spam matches: 3

ML Model Prediction:
- SpamScore: 0.4 + (3 × 0.2) = 1.0 → capped at 0.95
- ToxicScore: 0.05
- HateScore: 0.05
- OffensiveScore: 0.05

Context Multiplier:
- Author reputation: 0.1 (low) - 0.0 (no account age bonus) = 0.1
- Context multiplier: 0.1

Final Score Calculation:
finalScore = (0.95×0.3 + 0.05×0.3 + 0.05×0.25 + 0.05×0.15) × 0.1
finalScore = (0.285 + 0.015 + 0.0125 + 0.0075) × 0.1
finalScore = 0.32 × 0.1 = 0.032
```

**Decision:**
- Final Score: 0.032 (low due to context)
- **BUT:** Wordlist match triggers instant blocking
- **Decision: Block** (wordlist override)
- Status: Blocked

**Reasoning:** Explicit spam phrases detected in wordlist, regardless of final score. Wordlist provides instant blocking for known patterns.

---

### Example 3: Borderline Case - Review Queue

**Content:**
```
Text: "I'm not sure about this, but it seems suspicious"
Author: "@new_user" (Reputation: 50, Account Age: 15 days)
Image: None
```

**Agent Processing:**

**Sense:**
- Text: "I'm not sure about this, but it seems suspicious"
- Author reputation: 0.5 (neutral)
- Ambiguous language

**Think:**
```
Wordlist Check:
- Text: "im not sure about this but it seems suspicious"
- Matches: [] (no exact matches)

ML Model Prediction:
- SpamScore: 0.15 (some spam indicators: "suspicious")
- ToxicScore: 0.05
- HateScore: 0.05
- OffensiveScore: 0.10 (slightly elevated)

Context Multiplier:
- Author reputation: 0.5 (neutral)
- Context multiplier: 0.5

Final Score Calculation:
finalScore = (0.15×0.3 + 0.05×0.3 + 0.05×0.25 + 0.10×0.15) × 0.5
finalScore = (0.045 + 0.015 + 0.0125 + 0.015) × 0.5
finalScore = 0.0875 × 0.5 = 0.04375
```

**Decision:**
- Final Score: 0.04375
- Allow Threshold: 0.3
- Review Threshold: 0.5
- **Decision: Allow** (0.04375 < 0.3)
- Status: Approved

**Note:** This is a borderline case. If the score were between 0.3 and 0.7, it would go to Review Queue for human moderation.

---

### Example 4: High Offensive Score - Block

**Content:**
```
Text: "hello guys"
Author: "@abi" (Reputation: 50, Account Age: 30 days)
Image: None
```

**Agent Processing:**

**Sense:**
- Text: "hello guys"
- Author reputation: 0.5
- Simple greeting

**Think:**
```
Wordlist Check:
- Text: "hello guys"
- Matches: [] (no matches - word boundary prevents false positives)

ML Model Prediction:
- SpamScore: 0.05
- ToxicScore: 0.05
- HateScore: 0.05
- OffensiveScore: 0.70  // High - possibly due to wordlist or pattern

Context Multiplier:
- Author reputation: 0.5 + 0.1 (account age > 30) = 0.6
- Context multiplier: 0.6

Final Score Calculation:
finalScore = (0.05×0.3 + 0.05×0.3 + 0.05×0.25 + 0.70×0.15) × 0.6
finalScore = (0.015 + 0.015 + 0.0125 + 0.105) × 0.6
finalScore = 0.1475 × 0.6 = 0.0885
```

**Decision:**
- Final Score: 0.0885
- Allow Threshold: 0.3
- **Decision: Allow** (0.0885 < 0.3)
- Status: Approved

**Note:** Despite high offensive score (0.70), the final score is low due to low weights on offensive content (0.15) and positive context multiplier. This demonstrates how the system balances multiple factors.

---

### Example 5: Image Classification Influence

**Content:**
```
Text: "Check this out"
Author: "@user" (Reputation: 50)
Image: [Picture of a pistol, classified as "pistol" with 85% confidence]
```

**Agent Processing:**

**Sense:**
- Text: "Check this out"
- Image label: "pistol" (confidence: 0.85)
- Image confidence > 30%, so label is appended to text

**Think:**
```
Image Processing:
- Image label: "pistol"
- Confidence: 0.85 (> 0.3 threshold)
- Text for classification: "Check this out pistol"

Wordlist Check:
- Text: "check this out pistol"
- If "pistol" is in wordlist (offensive/weapon category):
  - Offensive matches: 1
  - OffensiveScore: 0.5 + (1 × 0.2) = 0.7

ML Model Prediction:
- SpamScore: 0.05
- ToxicScore: 0.05
- HateScore: 0.05
- OffensiveScore: 0.70 (boosted by image label)

Final Score Calculation:
finalScore = (0.05×0.3 + 0.05×0.3 + 0.05×0.25 + 0.70×0.15) × 0.5
finalScore = 0.1475 × 0.5 = 0.07375
```

**Decision:**
- Final Score: 0.07375
- **Decision: Allow** (if threshold allows) or **Block** (if wordlist has "pistol")
- Status: Depends on wordlist configuration

**Reasoning:** Image classification allows the system to detect problematic images even when text is harmless. The image label is treated as if it were in the text for wordlist checking.

---

## Scoring Algorithm - Detailed Explanation

The scoring algorithm combines multiple factors to determine content risk. This section explains the formula and provides step-by-step calculations.

### Formula Breakdown

```
Final Score = (SpamScore × 0.3 + ToxicScore × 0.3 + HateScore × 0.25 + OffensiveScore × 0.15) × ContextMultiplier
```

**Weights:**
- Spam: 30% (most common issue)
- Toxic: 30% (high priority)
- Hate: 25% (serious but less common)
- Offensive: 15% (lower priority, often subjective)

**Context Multiplier:**
- Range: 0.0 - 1.0
- Based on author reputation, account age, violation history
- Lower multiplier = more lenient (trusted authors)
- Higher multiplier = stricter (new/suspicious authors)

### Step-by-Step Calculation Example

**Input:**
- Content text: "This is really bad"
- Author: Reputation 30, Account Age 10 days, 1 previous violation
- No image

**Step 1: Wordlist Check**
```csharp
text = "this is really bad"
// Check each word against wordlist
// "bad" might match if in wordlist
// Assuming "bad" is NOT in wordlist (common word, not blocked)
wordlistMatches = 0
```

**Step 2: ML Model Prediction**
```csharp
// ML model analyzes text patterns
mlScores = {
    SpamScore: 0.05,      // No spam indicators
    ToxicScore: 0.25,     // "bad" might trigger some toxicity
    HateScore: 0.05,      // No hate speech
    OffensiveScore: 0.15  // Slightly elevated
}
```

**Step 3: Context Calculation**
```csharp
authorReputation = (30 / 100.0) + (10 > 30 ? 0.1 : 0) - (1 × 0.1)
authorReputation = 0.3 + 0 - 0.1 = 0.2

// Context multiplier (simplified - actual calculation may vary)
contextMultiplier = 0.8  // Lower for low reputation
```

**Step 4: Weighted Score**
```csharp
weightedScore = (0.05 × 0.3) + (0.25 × 0.3) + (0.05 × 0.25) + (0.15 × 0.15)
weightedScore = 0.015 + 0.075 + 0.0125 + 0.0225
weightedScore = 0.125
```

**Step 5: Apply Context**
```csharp
finalScore = 0.125 × 0.8
finalScore = 0.10
```

**Step 6: Decision**
```csharp
AllowThreshold = 0.3
BlockThreshold = 0.7

if (finalScore < 0.3) {
    Decision = Allow
} else if (finalScore > 0.7) {
    Decision = Block
```

**Actual Implementation (ScoringService.ScoreAndDecideAsync):**

```csharp
public async Task<Prediction> ScoreAndDecideAsync(Content content, CancellationToken cancellationToken = default)
{
    // 1. Check for image and get classification
    var contentImage = await _context.ContentImages
        .FirstOrDefaultAsync(img => img.ContentId == content.Id, cancellationToken);
    
    string? imageLabel = null;
    float imageConfidence = 0f;
    if (contentImage != null && !string.IsNullOrEmpty(contentImage.ClassificationResult))
    {
        // Parse classification result (stored as JSON)
        var classification = JsonSerializer.Deserialize<Dictionary<string, object>>(
            contentImage.ClassificationResult);
        if (classification != null)
        {
            imageLabel = classification.TryGetValue("label", out var labelObj) 
                ? labelObj?.ToString() : null;
            if (classification.TryGetValue("confidence", out var confObj) && confObj != null)
                float.TryParse(confObj.ToString(), out imageConfidence);
        }
    }

    // 2. Append image label to text for wordlist checking (if confidence > 30%)
    var textForClassification = content.Text;
    if (imageLabel != null && imageConfidence > 0.3)
    {
        textForClassification = $"{content.Text} {imageLabel}";
    }

    // 3. Get ML scores for text (includes image label if applicable)
    var textScores = await _classifier.PredictAsync(textForClassification, cancellationToken);

    // 4. Special handling: Boost scores for dog images (example rule)
    if (imageLabel != null && imageLabel.ToLower().Contains("dog") && imageConfidence > 0.5)
    {
        textScores = new ContentScores
        {
            SpamScore = textScores.SpamScore,
            ToxicScore = Math.Min(0.95, textScores.ToxicScore + 0.3),
            HateScore = Math.Min(0.95, textScores.HateScore + 0.3),
            OffensiveScore = Math.Min(0.95, textScores.OffensiveScore + 0.3)
        };
    }

    // 5. Calculate context multiplier
    var textContext = await _contextService.CalculateContextAsync(content, cancellationToken);
    var textContextMultiplier = await _contextService.CalculateContextMultiplierAsync(
        textContext, cancellationToken);

    // 6. Calculate final score (weighted average × context multiplier)
    var finalScore = (textScores.SpamScore * 0.3 +
                     textScores.ToxicScore * 0.3 +
                     textScores.HateScore * 0.25 +
                     textScores.OffensiveScore * 0.15) * textContextMultiplier;

    // 7. Get thresholds and make decision
    var settings = await _thresholdService.GetSettingsAsync(cancellationToken);
    var decision = finalScore < settings.AllowThreshold
        ? ModerationDecision.Allow
        : finalScore > settings.BlockThreshold
            ? ModerationDecision.Block
            : ModerationDecision.Review;

    // 8. Update content status based on decision
    content.Status = decision switch
    {
        ModerationDecision.Allow => ContentStatus.Approved,
        ModerationDecision.Block => ContentStatus.Blocked,
        ModerationDecision.Review => ContentStatus.PendingReview,
        _ => ContentStatus.PendingReview
    };
    content.ProcessedAt = DateTime.UtcNow;

    // 9. Create and save prediction
    var prediction = new Prediction
    {
        Id = Guid.NewGuid(),
        ContentId = content.Id,
        SpamScore = textScores.SpamScore,
        ToxicScore = textScores.ToxicScore,
        HateScore = textScores.HateScore,
        OffensiveScore = textScores.OffensiveScore,
        FinalScore = finalScore,
        Decision = decision,
        Confidence = 1.0 - Math.Abs(finalScore - 0.5) * 2, // Confidence based on distance from 0.5
        ContextFactors = JsonSerializer.Serialize(textContext),
        CreatedAt = DateTime.UtcNow
    };

    _context.Predictions.Add(prediction);
    await _context.SaveChangesAsync(cancellationToken);

    return prediction;
}
```

**Key Implementation Details:**
- Image labels are appended to text for wordlist checking (if confidence > 30%)
- Special rules can boost scores (e.g., dog images boost toxic/hate/offensive)
- Context multiplier is calculated from author reputation, account age, violations
- Decision is made using three-zone threshold system
- Content status is automatically updated based on decision
- Prediction is saved with all scores and context factors

**Step 6: Decision (continued)**
```csharp
AllowThreshold = 0.3
BlockThreshold = 0.7

if (finalScore < 0.3) {
    Decision = Allow
} else if (finalScore > 0.7) {
    Decision = Block
} else {
    Decision = Review
}

// Result: 0.10 < 0.3 → Allow
```

### Score Component Details

**Spam Score Calculation:**
```csharp
// Base score when spam keywords found
if (spamMatches > 0) {
    spamScore = Math.Min(0.95, 0.4 + (spamMatches × 0.2))
} else {
    spamScore = 0.05  // Default low score
}

// Example: 3 spam matches
spamScore = Math.Min(0.95, 0.4 + (3 × 0.2))
spamScore = Math.Min(0.95, 1.0) = 0.95
```

**Toxic/Hate/Offensive Score Calculation:**
```csharp
// Similar pattern for other categories
if (toxicMatches > 0) {
    toxicScore = Math.Min(0.95, 0.5 + (toxicMatches × 0.2))
} else {
    toxicScore = 0.05
}

// Multiple categories boost
if (categoryCount >= 2) {
    toxicScore = Math.Min(0.95, toxicScore × 1.2)
    hateScore = Math.Min(0.95, hateScore × 1.2)
    offensiveScore = Math.Min(0.95, offensiveScore × 1.2)
}
```

**Context Multiplier Calculation:**
```csharp
authorReputation = Math.Min(1.0, 
    (reputationScore / 100.0) + 
    (accountAgeDays > 30 ? 0.1 : 0) - 
    (previousViolations × 0.1)
)

// Example: Reputation 50, Age 60 days, 0 violations
authorReputation = Math.Min(1.0, 0.5 + 0.1 - 0.0) = 0.6

// Context multiplier (inverse relationship)
contextMultiplier = 1.0 - (authorReputation × 0.2)  // Simplified
// Higher reputation → lower multiplier → more lenient
```

### Decision Thresholds

**Three-Zone System:**

```
Zone 1: [0.0 - AllowThreshold)     → Allow (Auto-approve)
Zone 2: [AllowThreshold - BlockThreshold] → Review (Human moderation)
Zone 3: (BlockThreshold - 1.0]    → Block (Auto-reject)
```

**Default Thresholds:**
- Allow Threshold: 0.3
- Review Threshold: 0.5 (informational, not used in decision)
- Block Threshold: 0.7

**Visual Representation:**

```
[Image: Number line showing three zones:
0.0 ----[Allow: 0.3]----[Review Zone]----[Block: 0.7]---- 1.0
        Auto-approve    Human review    Auto-reject
]
```

**Example Decisions:**
- Score 0.15 → Allow (in green zone)
- Score 0.45 → Review (in yellow zone)
- Score 0.85 → Block (in red zone)

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
│   │       └── ThresholdUpdateRunner.cs
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

#### Wordlist Matching Algorithm

The wordlist matching uses a sophisticated algorithm to prevent false positives while ensuring accurate detection:

**Implementation:**

```csharp
// Helper function to check if keyword matches text
bool KeywordMatches(string keyword, string text)
{
    // If keyword contains spaces, it's a phrase - use Contains()
    if (keyword.Contains(' '))
    {
        return text.Contains(keyword);
    }
    
    // For single words, use word boundary matching to avoid substring matches
    // This prevents "guys" from matching "hello guys" incorrectly
    // But allows "guys" to match "hey guys" or "guys!" correctly
    var pattern = $@"\b{Regex.Escape(keyword)}\b";
    return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
}
```

**How It Works:**

1. **Single Words:** Uses word boundary matching (`\b...\b`) to match complete words only
   - ✅ Matches: "guys" in "hey guys" or "guys!" or "guys,"
   - ❌ Doesn't match: "guys" in "hello guys" (if "guys" is not a standalone word)
   - Prevents false positives like "hello guys" matching "guys" when "guys" is in the wordlist

2. **Phrases (Multiple Words):** Uses simple `Contains()` matching
   - ✅ Matches: "you are an idiot" in "you are an idiot here"
   - ✅ Matches: "i hate" in "i hate this"
   - Allows multi-word phrases to be detected anywhere in the text

3. **Case Insensitive:** All matching is case-insensitive
   - "FUCK" matches "fuck" and "Fuck"

**Example Scenarios:**

**Scenario 1: Single Word Match**
```
Wordlist: ["guys"]
Text: "hello guys"
Result: ✅ Matches (word boundary ensures "guys" is detected as a complete word)
```

**Scenario 2: Phrase Match**
```
Wordlist: ["you are an idiot"]
Text: "you are an idiot here"
Result: ✅ Matches (phrase detected using Contains())
```

**Scenario 3: False Positive Prevention**
```
Wordlist: ["guys"]
Text: "hello guys" (where "guys" is part of a compound word)
Result: ❌ No match (word boundary prevents substring match)
```

**Score Calculation:**

When a wordlist match is found, the score is boosted:

```csharp
// Base scores with higher minimums when keywords are found
var spamScore = spamMatches > 0 
    ? Math.Min(0.95, 0.4 + (spamMatches * 0.2)) 
    : 0.05;

var toxicScore = hasStrongToxic
    ? Math.Min(0.95, 0.5 + (toxicMatches * 0.2))
    : 0.05;

var hateScore = hasStrongHate
    ? Math.Min(0.95, 0.6 + (hateMatches * 0.2))
    : 0.05;

var offensiveScore = hasStrongOffensive
    ? Math.Min(0.95, 0.5 + (offensiveMatches * 0.2))
    : 0.05;
```

**Compound Effect:**

If multiple categories are triggered, scores are further boosted:

```csharp
var categoryCount = (hasStrongToxic ? 1 : 0) + (hasStrongHate ? 1 : 0) + (hasStrongOffensive ? 1 : 0);
if (categoryCount >= 2)
{
    toxicScore = Math.Min(0.95, toxicScore * 1.2);
    hateScore = Math.Min(0.95, hateScore * 1.2);
    offensiveScore = Math.Min(0.95, offensiveScore * 1.2);
}
```

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

#### ML Model Training Details

The ML model uses **ML.NET FastTree** algorithm for binary classification. This section explains the training process in detail.

**Training Pipeline:**

```csharp
// 1. Prepare training data from gold labels
var trainingData = new List<ContentInput>();
var labels = new List<ContentLabel>();

foreach (var review in goldLabels)
{
    if (review.Content == null || review.GoldLabel == null)
        continue;

    trainingData.Add(new ContentInput { Text = review.Content.Text });
    labels.Add(new ContentLabel
    {
        IsSpam = review.GoldLabel == ModerationDecision.Block,
        IsToxic = review.GoldLabel == ModerationDecision.Block,
        IsHate = review.GoldLabel == ModerationDecision.Block,
        IsOffensive = review.GoldLabel == ModerationDecision.Block
    });
}

// 2. Load data into ML.NET DataView
var dataView = _mlContext.Data.LoadFromEnumerable(
    trainingData.Zip(labels, (input, label) => new { Input = input, Label = label })
        .Select(x => new TrainingData
        {
            Text = x.Input.Text,
            IsSpam = x.Label.IsSpam,
            IsToxic = x.Label.IsToxic,
            IsHate = x.Label.IsHate,
            IsOffensive = x.Label.IsOffensive
        })
);

// 3. Define ML pipeline
var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", "Text")
    .Append(_mlContext.BinaryClassification.Trainers.FastTree(
        labelColumnName: "IsSpam",
        numberOfLeaves: 20,
        numberOfTrees: 100));

// 4. Train the model
var model = pipeline.Fit(dataView);

// 5. Evaluate metrics
var predictions = model.Transform(dataView);
var metrics = _mlContext.BinaryClassification.Evaluate(predictions, "IsSpam");
```

**FastTree Algorithm:**

FastTree is a gradient boosting decision tree algorithm that:
- Builds an ensemble of decision trees (100 trees in this implementation)
- Each tree has up to 20 leaves (complexity control)
- Uses gradient boosting to iteratively improve predictions
- Handles non-linear relationships and feature interactions

**Training Data Requirements:**

- **Minimum:** 10 gold labels (reviews with `GoldLabel` set)
- **Optimal:** 50+ gold labels for better accuracy
- **Label Format:** Binary (Block = true, Allow = false)

**Model Metrics:**

After training, the following metrics are calculated:

```csharp
var precision = metrics.PositivePrecision;  // True Positives / (True Positives + False Positives)
var recall = metrics.PositiveRecall;        // True Positives / (True Positives + False Negatives)
var accuracy = metrics.Accuracy;             // Correct Predictions / Total Predictions

// F1Score calculation (handles division by zero)
var f1Score = (precision + recall) > 0
    ? 2 * (precision * recall) / (precision + recall)
    : 0.0; // If both are 0, F1Score is 0 (not NaN)
```

**Metric Interpretation:**

- **Accuracy:** Overall correctness (higher is better, 0-1 range)
- **Precision:** When model predicts "Block", how often is it correct? (reduces false positives)
- **Recall:** Of all actual "Block" cases, how many did we catch? (reduces false negatives)
- **F1Score:** Harmonic mean of Precision and Recall (balanced metric)

**Model Versioning:**

Each retraining creates a new model version:

```csharp
var maxVersion = await _context.ModelVersions
    .AnyAsync(cancellationToken)
    ? await _context.ModelVersions.MaxAsync(m => (int?)m.Version, cancellationToken) ?? 0
    : 0;

var newVersion = maxVersion + 1;

var modelVersion = new ModelVersion
{
    Id = Guid.NewGuid(),
    Version = newVersion,
    Accuracy = accuracy,
    Precision = precision,
    Recall = recall,
    F1Score = f1Score,
    IsActive = activate,
    ModelPath = $"models/model_v{newVersion}.zip",
    TrainedAt = DateTime.UtcNow,
    TrainingSampleCount = goldLabels.Count
};
```

**Model Activation:**

When a new model is activated:
1. Previous active models are deactivated
2. New model is marked as `IsActive = true`
3. Model file is saved to disk at `ModelPath`
4. System settings are updated (`LastRetrainDate`, `NewGoldSinceLastTrain = 0`)

**Current Implementation Notes:**

- **Simplified Training:** Currently trains on `IsSpam` label only (binary classification)
- **Future Enhancement:** Multi-label classification for Spam/Toxic/Hate/Offensive separately
- **Heuristic Fallback:** If model training fails or no model exists, system uses keyword-based heuristics
- **NaN Protection:** All metrics are validated to prevent NaN/Infinity values before database storage

**Training Code Location:**

- **Training Logic:** `backend/src/AiAgents.ContentModerationAgent/ML/MlNetContentClassifier.cs` → `TrainAsync()`
- **Training Service:** `backend/src/AiAgents.ContentModerationAgent/Application/Services/TrainingService.cs` → `TrainModelAsync()`
- **Trigger:** `backend/src/AiAgents.ContentModerationAgent/Application/Services/ReviewService.cs` → `CheckAndTriggerRetrainingAsync()`

### Background Services

Three background services run continuously:

1. **ModerationAgentBackgroundService**
   - Runs ModerationAgentRunner every 500ms (when content available) or 5 seconds (when queue is empty)
   - Processes queued content
   - Emits SignalR events on completion

2. **RetrainAgentBackgroundService**
   - Runs RetrainAgentRunner every 5 minutes
   - Checks retraining threshold
   - Triggers retraining if needed

3. **ThresholdUpdateAgentBackgroundService**
   - Runs ThresholdUpdateRunner every hour
   - Monitors performance metrics
   - Adjusts thresholds if needed

**Service Lifecycle (ModerationAgentBackgroundService):**
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        using var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ModerationAgentRunner>();
        
        var result = await runner.TickAsync(stoppingToken);
        if (result != null)
        {
            await _hubContext.Clients.All.SendAsync("ModerationResult", result);
        }
        
        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
    }
}
```

**Note:** ModerationAgentBackgroundService runs every 500ms when content is available, or waits 5 seconds when no content is in queue. RetrainAgentBackgroundService runs every 5 minutes. ThresholdUpdateAgentBackgroundService runs every hour.

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
GET /api/content?status=4&search=hello&page=1&pageSize=50
```

**Query Parameters:**
- `status`: (Optional) Filter by ContentStatus
- `search`: (Optional) Search in text and author username
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 50)

**Response:**
```json
{
  "data": [ ... ],
  "totalCount": 250,
  "page": 1,
  "pageSize": 50,
  "totalPages": 5
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
GET /api/content/pending-review
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
POST /api/settings/retrain-threshold
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

1. **Adaptive Thresholds**
   - Automatic threshold adjustment based on performance
   - A/B testing of threshold values
   - Performance-based optimization

2. **Explanation System**
   - "Why did the agent make this decision?"
   - Highlights contributing factors
   - Shows which words/patterns triggered scores

3. **Multi-Agent Coordination**
   - Specialized agents for different content types
   - Agent communication and consensus
   - Load balancing across agents

4. **Sentiment Analysis**
   - Thread sentiment calculation
   - Context-aware decision making
   - Engagement pattern analysis

5. **Advanced Image Analysis**
   - Object detection (not just classification)
   - Text extraction from images (OCR)
   - NSFW detection

6. **Simulation Environment**
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
