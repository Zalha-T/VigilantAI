# Folder Reorganization Guide

## ⚠️ IMPORTANT: Read this before moving folders!

This guide explains how to safely reorganize the project structure from:
```
AI Agent/
├── src/                    ← Move this
├── frontend/
└── ...
```

To:
```
AI Agent/
├── backend/                ← New structure
│   └── src/               ← Moved from root
├── frontend/
└── ...
```

## Step-by-Step Instructions

### 1. **Backup First!**
   - Commit all changes to Git (or create a backup)
   - Ensure you have a clean working directory

### 2. **Files That Need Updates**

#### A. `AiAgents.sln` (Solution File)
   **Current paths:**
   ```
   src\AiAgents.Core\AiAgents.Core.csproj
   src\AiAgents.ContentModerationAgent\AiAgents.ContentModerationAgent.csproj
   src\AiAgents.ContentModerationAgent.Web\AiAgents.ContentModerationAgent.Web.csproj
   ```
   
   **New paths (after move):**
   ```
   backend\src\AiAgents.Core\AiAgents.Core.csproj
   backend\src\AiAgents.ContentModerationAgent\AiAgents.ContentModerationAgent.csproj
   backend\src\AiAgents.ContentModerationAgent.Web\AiAgents.ContentModerationAgent.Web.csproj
   ```
   
   **Action:** Update all 3 project paths in `AiAgents.sln`

#### B. `src/AiAgents.ContentModerationAgent.Web/Program.cs`
   **Current paths:**
   - Line 68: `new MlNetContentClassifier("models", sp)` - relative path
   - Line 97: `var modelsDir = Path.Combine(projectRoot, "models");`
   - Line 101: Hardcoded path: `@"C:\Users\HOME\Desktop\AI Agent\src\AiAgents.ContentModerationAgent.Web\models\resnet50-v2-7.onnx"`
   
   **Action:** 
   - The relative paths should still work (they're relative to project root)
   - Remove or update the hardcoded path on line 101
   - Models folder is at: `backend/src/AiAgents.ContentModerationAgent.Web/models/`

#### C. `.gitignore`
   **Current path:**
   ```
   src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/*
   !src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/.gitkeep
   ```
   
   **New path:**
   ```
   backend/src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/*
   !backend/src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/.gitkeep
   ```

#### D. `README.md` and Documentation Files
   - Update any paths that reference `src/` to `backend/src/`
   - Check: `API_USAGE_GUIDE.md`, `TESTING_GUIDE.md`, etc.

### 3. **Actual Move Steps**

#### Option A: Using File Explorer (Windows)
1. Create `backend` folder in root
2. Move `src` folder into `backend` folder
3. Update files listed above
4. Test build: `dotnet build AiAgents.sln`

#### Option B: Using Git (Recommended)
```bash
# 1. Create backend folder
mkdir backend

# 2. Move src to backend (Git will track the move)
git mv src backend/src

# 3. Update AiAgents.sln
# (Edit manually or use search/replace)

# 4. Update .gitignore
# (Edit manually)

# 5. Update Program.cs (remove hardcoded path)
# (Edit manually)

# 6. Test build
dotnet build AiAgents.sln

# 7. Commit changes
git add .
git commit -m "Reorganize: Move src/ to backend/src/"
```

### 4. **Files to Update Manually**

#### `AiAgents.sln` - Update these 3 lines:
```diff
- Project(...) = "AiAgents.Core", "src\AiAgents.Core\AiAgents.Core.csproj", ...
+ Project(...) = "AiAgents.Core", "backend\src\AiAgents.Core\AiAgents.Core.csproj", ...

- Project(...) = "AiAgents.ContentModerationAgent", "src\AiAgents.ContentModerationAgent\AiAgents.ContentModerationAgent.csproj", ...
+ Project(...) = "AiAgents.ContentModerationAgent", "backend\src\AiAgents.ContentModerationAgent\AiAgents.ContentModerationAgent.csproj", ...

- Project(...) = "AiAgents.ContentModerationAgent.Web", "src\AiAgents.ContentModerationAgent.Web\AiAgents.ContentModerationAgent.Web.csproj", ...
+ Project(...) = "AiAgents.ContentModerationAgent.Web", "backend\src\AiAgents.ContentModerationAgent.Web\AiAgents.ContentModerationAgent.Web.csproj", ...
```

#### `.gitignore` - Update path:
```diff
- src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/*
- !src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/.gitkeep
+ backend/src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/*
+ !backend/src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/.gitkeep
```

#### `src/AiAgents.ContentModerationAgent.Web/Program.cs` - Remove hardcoded path:
```diff
- var knownModelPath = @"C:\Users\HOME\Desktop\AI Agent\src\AiAgents.ContentModerationAgent.Web\models\resnet50-v2-7.onnx";
+ // Removed hardcoded path - using relative path instead
```

### 5. **Testing After Move**

1. **Build test:**
   ```bash
   dotnet build AiAgents.sln
   ```

2. **Run test:**
   ```bash
   cd backend/src/AiAgents.ContentModerationAgent.Web
   dotnet run
   ```

3. **Check paths:**
   - Models folder: `backend/src/AiAgents.ContentModerationAgent.Web/models/`
   - Uploads folder: `backend/src/AiAgents.ContentModerationAgent.Web/wwwroot/uploads/images/`

### 6. **What Should NOT Break**

✅ Relative paths in code (they're relative to project root)
✅ Database connection strings
✅ Frontend (it's separate)
✅ NuGet packages (they're in project files)

### 7. **What MIGHT Break**

⚠️ Hardcoded absolute paths (check `Program.cs` line 101)
⚠️ Documentation that references `src/` paths
⚠️ CI/CD scripts (if you have any)

### 8. **Final Checklist**

- [ ] Moved `src/` to `backend/src/`
- [ ] Updated `AiAgents.sln` (3 project paths)
- [ ] Updated `.gitignore` (uploads path)
- [ ] Removed hardcoded path in `Program.cs`
- [ ] Updated documentation files
- [ ] Tested `dotnet build AiAgents.sln`
- [ ] Tested `dotnet run` (backend starts)
- [ ] Tested image upload (uploads folder works)
- [ ] Committed changes to Git

## Quick Reference: New Structure

```
AI Agent/
├── backend/
│   └── src/
│       ├── AiAgents.Core/
│       ├── AiAgents.ContentModerationAgent/
│       └── AiAgents.ContentModerationAgent.Web/
│           ├── models/              ← ONNX model here
│           └── wwwroot/
│               └── uploads/
│                   └── images/      ← Uploaded images here
├── frontend/
├── AiAgents.sln                     ← Updated paths
└── .gitignore                       ← Updated paths
```

## Need Help?

If something breaks:
1. Check `dotnet build` errors - they'll tell you which paths are wrong
2. Check logs when running - they show actual paths being used
3. Verify relative paths are relative to project root, not solution root
