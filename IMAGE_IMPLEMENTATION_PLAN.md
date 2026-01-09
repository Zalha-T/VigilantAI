# Image Classification Implementation Plan

## 🎯 Goal
Add image classification to GuardianAI using **local pretrained model** (no external services). Images are **nullable** to maintain backward compatibility.

## 📋 Implementation Steps

### Phase 1: Backend - Domain & Infrastructure

#### Step 1: Create ContentImage Entity
- **File**: `src/AiAgents.ContentModerationAgent/Domain/Entities/ContentImage.cs`
- **Properties**:
  - `Id` (Guid)
  - `ContentId` (Guid, FK to Content)
  - `FileName` (string) - stored filename
  - `OriginalFileName` (string) - original upload name
  - `FilePath` (string) - relative path to image
  - `MimeType` (string) - image/jpeg, image/png, etc.
  - `FileSize` (long) - bytes
  - `ClassificationResult` (string?) - JSON: `{"label": "dog", "confidence": 0.95, "isBlocked": true}`
  - `CreatedAt` (DateTime)
- **Navigation**: `Content Content` (nullable)

#### Step 2: Update DbContext
- **File**: `src/AiAgents.ContentModerationAgent/Infrastructure/ContentModerationDbContext.cs`
- Add `DbSet<ContentImage> ContentImages`
- Configure entity in `OnModelCreating`

### Phase 2: Backend - ML Layer

#### Step 3: Add ML.NET Image Classification Packages
- **File**: `src/AiAgents.ContentModerationAgent/AiAgents.ContentModerationAgent.csproj`
- Add packages:
  ```xml
  <PackageReference Include="Microsoft.ML.Vision" Version="3.0.0" />
  <PackageReference Include="Microsoft.ML.ImageAnalytics" Version="3.0.0" />
  ```

#### Step 4: Create Image Classifier Interface
- **File**: `src/AiAgents.ContentModerationAgent/ML/IImageClassifier.cs`
- **Methods**:
  - `Task<ImageClassificationResult> ClassifyAsync(byte[] imageBytes, CancellationToken cancellationToken)`
- **ImageClassificationResult**:
  - `Label` (string) - "dog", "cat", "other"
  - `Confidence` (float) - 0.0-1.0
  - `IsBlocked` (bool) - true if dog detected

#### Step 5: Implement DogCatImageClassifier
- **File**: `src/AiAgents.ContentModerationAgent/ML/DogCatImageClassifier.cs`
- **Approach**: Use ML.NET Image Classification with transfer learning
- **Model**: 
  - Option A: Use pretrained ResNet50 (ImageNet) → classify as "dog" or "cat" or "other"
  - Option B: Quick training on small dataset (100-200 images)
- **Logic**:
  - If label contains "dog" → `IsBlocked = true`
  - If label contains "cat" → `IsBlocked = false`
  - Otherwise → `IsBlocked = false`

### Phase 3: Backend - Application Layer

#### Step 6: Create Image Storage Service
- **File**: `src/AiAgents.ContentModerationAgent/Application/Services/IImageStorageService.cs`
- **File**: `src/AiAgents.ContentModerationAgent/Application/Services/ImageStorageService.cs`
- **Methods**:
  - `Task<string> SaveImageAsync(byte[] imageBytes, string fileName, Guid contentId)`
  - `Task<byte[]?> GetImageAsync(string filePath)`
  - `Task<bool> DeleteImageAsync(string filePath)`
- **Storage**: Local folder `wwwroot/uploads/images/{contentId}/`

#### Step 7: Update ScoringService
- **File**: `src/AiAgents.ContentModerationAgent/Application/Services/ScoringService.cs`
- **Changes**:
  - Inject `IImageClassifier`
  - If content has images:
    - Classify each image
    - If any image is blocked (dog) → **override decision to Block**
    - Otherwise, combine image scores with text scores
  - Update `FinalScore` calculation

### Phase 4: Backend - Web Layer

#### Step 8: Update CreateContent Endpoint
- **File**: `src/AiAgents.ContentModerationAgent.Web/Controllers/ContentController.cs`
- **Changes**:
  - Change from `[FromBody]` to `[FromForm]` (multipart/form-data)
  - Accept `IFormFile? Image` (nullable)
  - If image provided:
    - Save image using `ImageStorageService`
    - Create `ContentImage` entity
    - Classify image using `IImageClassifier`
    - Store classification result

#### Step 9: Update GetContentById Endpoint
- **File**: `src/AiAgents.ContentModerationAgent.Web/Controllers/ContentController.cs`
- **Changes**:
  - Include `ContentImages` in query
  - Return image info + classification results:
    ```json
    {
      "images": [
        {
          "id": "...",
          "fileName": "image.jpg",
          "url": "/api/content/images/{contentId}/{imageId}",
          "classification": {
            "label": "dog",
            "confidence": 0.95,
            "isBlocked": true
          }
        }
      ]
    }
    ```

#### Step 10: Add Image Serving Endpoint
- **File**: `src/AiAgents.ContentModerationAgent.Web/Controllers/ContentController.cs`
- **Endpoint**: `GET /api/content/{contentId}/images/{imageId}`
- Returns image file with proper content-type

### Phase 5: Frontend

#### Step 11: Update CreateContent Page
- **File**: `frontend/src/pages/CreateContent.tsx`
- **Changes**:
  - Add file input for image upload
  - Preview uploaded image
  - Send as `FormData` instead of JSON
  - Update API call to use `multipart/form-data`

#### Step 12: Update ContentDetails Page
- **File**: `frontend/src/pages/ContentDetails.tsx`
- **Changes**:
  - Display images if available
  - Show classification results: **"Detected: dog (95% confidence) - BLOCKED"**
  - Show classification results: **"Detected: cat (87% confidence) - Allowed"**
  - Display image thumbnails with labels

#### Step 13: Update API Service
- **File**: `frontend/src/services/api.ts`
- **Changes**:
  - Update `CreateContentRequest` interface to include `image?: File`
  - Update `contentApi.create()` to use `FormData`
  - Add `ContentImage` interface
  - Update `Content` interface to include `images?: ContentImage[]`

## 🔧 Technical Details

### Image Storage Structure
```
wwwroot/
  uploads/
    images/
      {contentId}/
        {imageId}.jpg
```

### Classification Result Format
```json
{
  "label": "dog",
  "confidence": 0.95,
  "isBlocked": true,
  "details": "Detected: golden retriever (95% confidence)"
}
```

### Decision Logic
1. **Text only**: Use existing text classification
2. **Image only**: If dog → Block, else Allow
3. **Text + Image**: 
   - If image is dog → **Block** (override text)
   - Otherwise: Combine text + image scores

### Model Training (Optional)
- Use ML.NET Image Classification API
- Small dataset: 50-100 dog images, 50-100 cat images
- Transfer learning from ResNet50
- Save model to `models/image-classifier.zip`

## 📝 Notes

- **Images are nullable** - existing content without images continues to work
- **No seed data** - images added only through Create Content
- **Local storage** - images stored in `wwwroot/uploads/images/`
- **Classification happens on upload** - not during moderation tick
- **Dog = Blocked** - hard rule for demonstration
- **Cat/Other = Allowed** - unless text classification blocks it

## ✅ Acceptance Criteria

- [ ] Can upload image when creating content
- [ ] Image is saved to local storage
- [ ] Image is classified (dog/cat/other)
- [ ] Classification result is stored in database
- [ ] ContentDetails shows image and classification result
- [ ] Dog images automatically block content
- [ ] Cat images don't block content
- [ ] Existing content without images still works
- [ ] Backend logs classification results

## 🚀 Next Steps After Implementation

1. Test with various images (dog, cat, other)
2. Add image preview in Review Queue
3. Add image deletion functionality
4. Consider image compression/optimization
5. Add support for multiple images per content
