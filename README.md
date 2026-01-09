# VigilantAI - Content Moderation Agent

AI-powered content moderation system that automatically analyzes and classifies user-generated content (comments, posts, messages) using a combination of rule-based wordlist filtering and machine learning models that learn from moderator feedback.

## 🎯 What Makes This an Agent?

VigilantAI is a **multi-agent system** that operates autonomously through continuous cycles:

- **Moderation Agent** (Classification + Context-Aware): Perceives content, thinks using ML models and rules, acts by updating status, learns indirectly through feedback
- **Retraining Agent** (Learning): Monitors feedback, triggers model retraining, manages model versions
- **Threshold Update Agent** (Goal-Oriented): Optimizes decision thresholds based on performance metrics

Each agent follows the **Sense → Think → Act → Learn** cycle, making this a true software agent system, not just a classification tool.

## ✨ Features

- 🤖 **Autonomous Moderation**: Real-time content analysis and classification
- 📚 **Wordlist Filtering**: Instant blocking of explicit words/phrases
- 🧠 **ML Model Learning**: Adapts and improves from moderator feedback
- 🖼️ **Image Classification**: Analyzes images using ResNet50 ONNX model
- 📊 **Context-Aware Decisions**: Considers author reputation, thread context, engagement
- 🔄 **Continuous Learning**: Automatic model retraining when sufficient feedback is available
- 📱 **Modern Web UI**: React frontend with real-time updates via SignalR
- ⚙️ **Configurable Thresholds**: Adjustable decision boundaries
- 🎨 **Beautiful UI**: Gradient buttons, smooth animations, toast notifications

## 🏗️ Architecture

### Clean Architecture Layers

```
AiAgents.Core/                    # Framework abstractions
AiAgents.ContentModerationAgent/  # Shared agent logic
  ├── Domain/                     # Entities and business rules
  ├── Application/                # Use cases and agent runners
  ├── Infrastructure/             # Data access
  └── ML/                         # Machine learning components
AiAgents.ContentModerationAgent.Web/  # Host/Transport layer
```

### Agent Runners

- **ModerationAgentRunner**: Processes queued content (Sense → Think → Act)
- **RetrainAgentRunner**: Monitors and triggers retraining (Sense → Think → Act → Learn)
- **ThresholdUpdateAgentRunner**: Optimizes thresholds (Sense → Think → Act → Learn)

## 🚀 Quick Start

### Prerequisites

- .NET 8.0 SDK
- SQL Server (LocalDB, Express, or full)
- Node.js 18+ and npm

### Backend Setup

```bash
cd backend
dotnet restore
dotnet build backend/AiAgents.sln
cd src/AiAgents.ContentModerationAgent.Web
dotnet run
```

API available at `https://localhost:60830`  
Swagger UI: `https://localhost:60830/swagger`

### Frontend Setup

```bash
cd frontend
npm install
npm run dev
```

Frontend available at `http://localhost:3000`

### Database

Database is automatically created on first run. Update connection string in `appsettings.json` if needed.

### ONNX Model

Download `resnet50-v2-7.onnx` from [ONNX Model Zoo](https://github.com/onnx/models) and place in:
```
backend/src/AiAgents.ContentModerationAgent.Web/models/resnet50-v2-7.onnx
```

## 📖 Documentation

For comprehensive documentation, see [DOCUMENTATION.md](./DOCUMENTATION.md)

## 🎮 Usage

1. **Create Content**: Add new content through the web UI
2. **Review Queue**: Moderate content that needs human review
3. **Settings**: Configure thresholds and retraining parameters
4. **Wordlist**: Manage blocked words and phrases
5. **Dashboard**: View all content with real-time updates

## 🔧 Configuration

### Moderation Thresholds

- **Allow Threshold** (default: 0.3): Content below this is approved
- **Review Threshold** (default: 0.5): Content between Allow and Review goes to queue
- **Block Threshold** (default: 0.7): Content above this is blocked

### Retraining

- **Retrain Threshold** (default: 6): Number of new gold labels needed
- Automatically triggers when threshold is reached
- Requires minimum 10 gold labels in database

## 🧪 Testing

### API Testing

Use Swagger UI at `/swagger` to test all API endpoints interactively.

### Manual Testing

1. Create content through UI
2. Check dashboard for processed content
3. Submit reviews to generate gold labels
4. Monitor retraining in settings page

## 📁 Project Structure

```
VigilantAI/
├── backend/
│   └── src/
│       ├── AiAgents.Core/              # Framework
│       ├── AiAgents.ContentModerationAgent/  # Agent logic
│       └── AiAgents.ContentModerationAgent.Web/  # Web host
├── frontend/                           # React frontend
└── DOCUMENTATION.md                    # Full documentation
```

## 🔑 Key Concepts

### Wordlist vs ML Model

- **Wordlist**: Rule-based, instant blocking of explicit words
- **ML Model**: Learns patterns, adapts to new content types
- **Combined**: Wordlist for explicit blocking, ML for contextual understanding

### Scoring Formula

```
Final Score = (Spam×0.3 + Toxic×0.3 + Hate×0.25 + Offensive×0.15) × ContextMultiplier
```

### Decision Logic

- Final Score < Allow Threshold → **Allow**
- Final Score > Block Threshold → **Block**
- Otherwise → **Review** (human moderation)

## 🛠️ Technology Stack

**Backend:**
- .NET 8.0
- Entity Framework Core
- ML.NET
- ONNX Runtime
- SignalR

**Frontend:**
- React 18
- TypeScript
- Vite
- Axios
- SignalR Client

## 📝 License

This project is developed for educational purposes as part of the AI course.

## 🤝 Contributing

This is an academic project. For questions or issues, refer to the documentation or contact the course instructor.

---

**Version:** 1.0  
**Last Updated:** January 2026
