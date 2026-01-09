using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;
using System.Drawing;
using System.Drawing.Imaging;
using Image = System.Drawing.Image;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Linq;

namespace AiAgents.ContentModerationAgent.ML;

/// <summary>
/// Image classifier using pretrained ResNet50 ONNX model (ImageNet).
/// Classifies images into 1000 ImageNet categories without requiring training.
/// Uses ImageNet pretrained model for general image classification.
/// </summary>
public class ImageNetClassifier : IImageClassifier
{
    private readonly MLContext _mlContext;
    private readonly string _modelsDirectory;
    private ITransformer? _model;
    private InferenceSession? _onnxSession; // Direct ONNX Runtime session
    private const string ModelFileName = "resnet50-v2-7.onnx";
    private const string ModelUrl = "https://github.com/onnx/models/raw/main/validated/vision/classification/resnet/model/resnet50-v2-7.onnx";
    private readonly object _modelLock = new object();
    private readonly object _onnxSessionLock = new object();
    private readonly ILogger<ImageNetClassifier>? _logger;

    // ImageNet class indices for dogs (151-268) and cats (281-285)
    private static readonly HashSet<int> DogClassIndices = new()
    {
        151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170,
        171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190,
        191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210,
        211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230,
        231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 241, 242, 243, 244, 245, 246, 247, 248, 249, 250,
        251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268
    };

    private static readonly HashSet<int> CatClassIndices = new()
    {
        281, 282, 283, 284, 285
    };

    public ImageNetClassifier(string modelsDirectory = "models", ILogger<ImageNetClassifier>? logger = null)
    {
        _mlContext = new MLContext(seed: 0);
        _logger = logger;
        // Use absolute path to ensure we know where models are stored
        if (Path.IsPathRooted(modelsDirectory))
        {
            _modelsDirectory = Path.GetFullPath(modelsDirectory); // Normalize the path
        }
        else
        {
            // If relative path is provided, make it absolute based on ContentRootPath
            // This should be set from Program.cs using env.ContentRootPath
            // For now, use AppContext.BaseDirectory which points to the executing assembly location
            // In ASP.NET Core, this is usually bin/Debug/net8.0/, so we go up to find the project root
            var baseDir = AppContext.BaseDirectory;
            _logger?.LogInformation($"[ImageNetClassifier] AppContext.BaseDirectory: {baseDir}");
            
            // Try to find the project root by going up from bin/Debug/net8.0/
            // This should get us to src/AiAgents.ContentModerationAgent.Web/
            var projectRoot = baseDir;
            if (baseDir.Contains("bin"))
            {
                // Go up from bin/Debug/net8.0/ to project root
                var binIndex = baseDir.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
                projectRoot = baseDir.Substring(0, binIndex).TrimEnd('\\', '/');
            }
            
            _modelsDirectory = Path.GetFullPath(Path.Combine(projectRoot, modelsDirectory));
            _logger?.LogInformation($"[ImageNetClassifier] Project root inferred: {projectRoot}");
        }
        Directory.CreateDirectory(_modelsDirectory);
        var fullPath = Path.GetFullPath(_modelsDirectory);
        _logger?.LogInformation($"[DogCatImageClassifier] Constructor - Models directory set to: {fullPath}");
        _logger?.LogInformation($"[DogCatImageClassifier] Constructor - Directory exists: {Directory.Exists(fullPath)}");
        
        // Check if model exists immediately
        var modelPath = Path.Combine(fullPath, ModelFileName);
        _logger?.LogInformation($"[DogCatImageClassifier] Constructor - Model path: {modelPath}");
        _logger?.LogInformation($"[DogCatImageClassifier] Constructor - Model exists: {File.Exists(modelPath)}");
    }

    private async Task EnsureModelExistsAsync()
    {
        var modelPath = Path.Combine(_modelsDirectory, ModelFileName);
        var fullPath = Path.GetFullPath(modelPath);
        
        // Also check the known location
        var knownModelPath = @"C:\Users\HOME\Desktop\AI Agent\src\AiAgents.ContentModerationAgent.Web\models\resnet50-v2-7.onnx";
        
        _logger?.LogInformation($"[DogCatImageClassifier] Checking for model at: {fullPath}");
        _logger?.LogInformation($"[DogCatImageClassifier] Also checking known location: {knownModelPath}");
        _logger?.LogInformation($"[DogCatImageClassifier] Models directory: {_modelsDirectory}");
        _logger?.LogInformation($"[DogCatImageClassifier] Model file name: {ModelFileName}");
        _logger?.LogInformation($"[DogCatImageClassifier] Current working directory: {Directory.GetCurrentDirectory()}");
        
        // Check multiple locations
        string? actualModelPath = null;
        if (File.Exists(modelPath))
        {
            actualModelPath = modelPath;
        }
        else if (File.Exists(fullPath))
        {
            actualModelPath = fullPath;
        }
        else if (File.Exists(knownModelPath))
        {
            actualModelPath = knownModelPath;
            _logger?.LogInformation($"[ImageNetClassifier] Model found at known location: {knownModelPath}");
        }
        
        if (actualModelPath != null)
        {
            _logger?.LogInformation($"[ImageNetClassifier] ✓ Model found at {Path.GetFullPath(actualModelPath)}. Skipping download.");
            return;
        }
        
        _logger?.LogWarning($"[DogCatImageClassifier] ✗ Model NOT found. Checked:");
        _logger?.LogWarning($"[DogCatImageClassifier]   - {fullPath}");
        _logger?.LogWarning($"[DogCatImageClassifier]   - {knownModelPath}");
        _logger?.LogWarning($"[DogCatImageClassifier] Starting download...");

        // Try to download the model
        try
        {
            Console.WriteLine($"Downloading ONNX model from {ModelUrl}...");
            Console.WriteLine($"This may take a few minutes (model is ~100MB)...");
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(15); // Increased timeout for large file
            
            // Download with progress indication
            var response = await httpClient.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
            
            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                
                if (totalBytes > 0 && totalBytesRead % (10 * 1024 * 1024) == 0) // Log every 10MB
                {
                    var progress = (double)totalBytesRead / totalBytes * 100;
                    Console.WriteLine($"Download progress: {progress:F1}% ({totalBytesRead / (1024 * 1024)}MB / {totalBytes / (1024 * 1024)}MB)");
                }
            }
            
            Console.WriteLine($"ONNX model downloaded successfully to {modelPath}");
        }
        catch (Exception ex)
        {
            // If download fails, log but don't throw - we'll use fallback
            Console.WriteLine($"Warning: Could not download ONNX model: {ex.Message}");
            Console.WriteLine($"Please manually download {ModelUrl} and place it in {_modelsDirectory} folder.");
            Console.WriteLine($"Model directory: {Path.GetFullPath(_modelsDirectory)}");
        }
    }

    private ITransformer? GetOrCreateModel()
    {
        if (_model != null)
            return _model;

        lock (_modelLock)
        {
            if (_model != null)
                return _model;

            var modelPath = Path.Combine(_modelsDirectory, ModelFileName);
            var fullModelPath = Path.GetFullPath(modelPath);
            
            // Check both relative and absolute paths
            string? actualModelPath = null;
            if (File.Exists(modelPath))
            {
                actualModelPath = modelPath;
                _logger?.LogInformation($"[ImageNetClassifier] Found model at relative path: {modelPath}");
            }
            else if (File.Exists(fullModelPath))
            {
                actualModelPath = fullModelPath;
                _logger?.LogInformation($"[ImageNetClassifier] Found model at full path: {fullModelPath}");
            }
            else
            {
                // Try alternative locations
                var searchPaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "models", ModelFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src", "AiAgents.ContentModerationAgent.Web", "models", ModelFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "models", ModelFileName),
                    Path.Combine(_modelsDirectory, ModelFileName)
                };
                
                foreach (var altPath in searchPaths)
                {
                    var altFullPath = Path.GetFullPath(altPath);
                    Console.WriteLine($"[DogCatImageClassifier] Checking alternative path: {altFullPath}");
                    if (File.Exists(altFullPath))
                    {
                        actualModelPath = altFullPath;
                        Console.WriteLine($"[DogCatImageClassifier] Found model at alternative path: {altFullPath}");
                        break;
                    }
                }
            }
            
            if (actualModelPath == null)
            {
                Console.WriteLine($"[DogCatImageClassifier] Model not found. Searched:");
                Console.WriteLine($"[DogCatImageClassifier]   - {modelPath}");
                Console.WriteLine($"[DogCatImageClassifier]   - {fullModelPath}");
                Console.WriteLine($"[DogCatImageClassifier]   - {Path.Combine(Directory.GetCurrentDirectory(), "models", ModelFileName)}");
                return null;
            }
            
            Console.WriteLine($"[DogCatImageClassifier] Using model at: {Path.GetFullPath(actualModelPath)}");

            try
            {
                _logger?.LogInformation($"[ImageNetClassifier] Creating ML.NET pipeline...");
                // Create pipeline for ONNX model inference
                // Note: ML.NET OnnxTransformer requires specific input/output shapes
                // For ResNet50: input is [1, 3, 224, 224] (batch, channels, height, width)
                _logger?.LogInformation($"[ImageNetClassifier] Adding ONNX model to pipeline...");
                _logger?.LogInformation($"[ImageNetClassifier] Model file: {actualModelPath}");
                _logger?.LogInformation($"[ImageNetClassifier] Model file exists: {File.Exists(actualModelPath)}");
                
                var pipeline = _mlContext.Transforms
                    .LoadImages("Image", null, "ImagePath")
                    .Append(_mlContext.Transforms.ResizeImages("Image", 224, 224, "Image"))
                    .Append(_mlContext.Transforms.ExtractPixels("data", "Image", 
                        interleavePixelColors: true, 
                        offsetImage: 117f, 
                        scaleImage: 1f / 117f))
                    .Append(_mlContext.Transforms.ApplyOnnxModel(
                        modelFile: actualModelPath,
                        outputColumnNames: new[] { "output" },
                        inputColumnNames: new[] { "data" }));

                _logger?.LogInformation($"[ImageNetClassifier] Pipeline created. Creating dummy data...");
                // Create dummy data to fit the pipeline
                var tempImagePath = Path.Combine(Path.GetTempPath(), "temp_classify.jpg");
                if (!File.Exists(tempImagePath))
                {
                    // Create a dummy 224x224 image
                    using var dummyImage = new Bitmap(224, 224);
                    dummyImage.Save(tempImagePath, ImageFormat.Jpeg);
                }

                var data = new[] { new { ImagePath = tempImagePath } };
                var dataView = _mlContext.Data.LoadFromEnumerable(data);
                _logger?.LogInformation($"[ImageNetClassifier] Fitting pipeline with dummy data...");
                _model = pipeline.Fit(dataView);
                _logger?.LogInformation($"[ImageNetClassifier] ✓ Model loaded successfully in GetOrCreateModelWithPath!");
            }
            catch (TypeInitializationException tiex)
            {
                _logger?.LogError(tiex, $"[DogCatImageClassifier] TypeInitializationException loading ONNX model: {tiex.Message}");
                if (tiex.InnerException != null)
                {
                    _logger?.LogError($"[DogCatImageClassifier] Inner exception: {tiex.InnerException.Message}");
                    _logger?.LogError($"[DogCatImageClassifier] Inner exception type: {tiex.InnerException.GetType().FullName}");
                }
                _logger?.LogError($"[DogCatImageClassifier] This usually means ONNX Runtime native dependencies are missing.");
                _logger?.LogError($"[DogCatImageClassifier] ONNX Runtime requires native DLLs (onnxruntime.dll) which may not be available.");
                // Return null - we'll use fallback
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"[DogCatImageClassifier] Error loading ONNX model: {ex.Message}");
                _logger?.LogError($"[DogCatImageClassifier] Exception type: {ex.GetType().FullName}");
                if (ex.InnerException != null)
                {
                    _logger?.LogError($"[DogCatImageClassifier] Inner exception: {ex.InnerException.Message}");
                    _logger?.LogError($"[DogCatImageClassifier] Inner exception type: {ex.InnerException.GetType().FullName}");
                }
                _logger?.LogError($"[DogCatImageClassifier] Stack trace: {ex.StackTrace}");
                // Return null - we'll use fallback
                return null;
            }
        }

        return _model;
    }
    
    private ITransformer? GetOrCreateModelWithPath(string modelPath)
    {
        if (_model != null)
            return _model;

        lock (_modelLock)
        {
            if (_model != null)
                return _model;

            var fullModelPath = Path.GetFullPath(modelPath);
            
            // Check if model exists at the provided path
            if (!File.Exists(modelPath) && !File.Exists(fullModelPath))
            {
                _logger?.LogWarning($"[DogCatImageClassifier] Model not found at: {modelPath} or {fullModelPath}");
                return null;
            }
            
            var actualModelPath = File.Exists(modelPath) ? modelPath : fullModelPath;
            _logger?.LogInformation($"[ImageNetClassifier] Loading model from: {Path.GetFullPath(actualModelPath)}");

            try
            {
                _logger?.LogInformation($"[ImageNetClassifier] Creating ML.NET pipeline with proper preprocessing...");
                // ResNet50 ONNX model expects:
                // - Input shape: [1, 3, 224, 224] (batch, channels, height, width) - CHW format
                // - Pixel values normalized to [0, 1] range (divide by 255)
                // - BGR channel order (Blue, Green, Red) - ML.NET ExtractPixels uses RGB by default
                // Note: interleavePixelColors=false means CHW format (not HWC)
                var pipeline = _mlContext.Transforms
                    .LoadImages("Image", null, "ImagePath")
                    .Append(_mlContext.Transforms.ResizeImages("Image", 224, 224, "Image"))
                    .Append(_mlContext.Transforms.ExtractPixels("data", "Image", 
                        interleavePixelColors: false, // CHW format (channels, height, width) - required for ONNX
                        offsetImage: 0f, // No offset - normalize to 0-1
                        scaleImage: 1f / 255f, // Normalize pixel values from [0,255] to [0,1]
                        colorsToExtract: Microsoft.ML.Transforms.Image.ImagePixelExtractingEstimator.ColorBits.Rgb)) // RGB order (will be converted to BGR if needed)
                    .Append(_mlContext.Transforms.ApplyOnnxModel(
                        modelFile: actualModelPath,
                        outputColumnNames: new[] { "output" },
                        inputColumnNames: new[] { "data" }));
                
                _logger?.LogInformation($"[ImageNetClassifier] Pipeline created with: interleavePixelColors=false, offsetImage=0, scaleImage=1/255");

                // Create dummy data to fit the pipeline
                var tempImagePath = Path.Combine(Path.GetTempPath(), "temp_classify.jpg");
                if (!File.Exists(tempImagePath))
                {
                    // Create a dummy 224x224 image
                    using var dummyImage = new Bitmap(224, 224);
                    dummyImage.Save(tempImagePath, ImageFormat.Jpeg);
                }

                var data = new[] { new { ImagePath = tempImagePath } };
                var dataView = _mlContext.Data.LoadFromEnumerable(data);
                _logger?.LogInformation($"[ImageNetClassifier] Fitting pipeline with dummy data...");
                _model = pipeline.Fit(dataView);
                _logger?.LogInformation($"[ImageNetClassifier] ✓ Model loaded successfully in GetOrCreateModelWithPath!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DogCatImageClassifier] Error loading ONNX model: {ex.Message}");
                Console.WriteLine($"[DogCatImageClassifier] Stack trace: {ex.StackTrace}");
                // Return null - we'll use fallback
                return null;
            }
        }

        return _model;
    }

    public async Task<ImageClassificationResult> ClassifyAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation($"[DogCatImageClassifier] ========== ClassifyAsync STARTED ==========");
        _logger?.LogInformation($"[DogCatImageClassifier] Image size: {imageBytes.Length} bytes");
        _logger?.LogInformation($"[DogCatImageClassifier] Models directory: {Path.GetFullPath(_modelsDirectory)}");
        
        try
        {
            // Ensure model exists (download if needed)
            _logger?.LogInformation($"[ImageNetClassifier] Checking if ONNX model exists...");
            await EnsureModelExistsAsync();
            _logger?.LogInformation($"[ImageNetClassifier] EnsureModelExistsAsync completed.");

            // Try to use ONNX model if available
            var modelPath = Path.Combine(_modelsDirectory, ModelFileName);
            var fullModelPath = Path.GetFullPath(modelPath);
            var knownModelPath = @"C:\Users\HOME\Desktop\AI Agent\src\AiAgents.ContentModerationAgent.Web\models\resnet50-v2-7.onnx";
            
            _logger?.LogInformation($"[ImageNetClassifier] Looking for model at: {fullModelPath}");
            _logger?.LogInformation($"[ImageNetClassifier] Also checking known location: {knownModelPath}");
            _logger?.LogInformation($"[ImageNetClassifier] File exists (relative): {File.Exists(modelPath)}");
            _logger?.LogInformation($"[ImageNetClassifier] File exists (full): {File.Exists(fullModelPath)}");
            _logger?.LogInformation($"[ImageNetClassifier] File exists (known): {File.Exists(knownModelPath)}");
            
            // Try to find model in alternative locations
            string? actualModelPath = null;
            if (File.Exists(modelPath))
            {
                actualModelPath = modelPath;
                _logger?.LogInformation($"[ImageNetClassifier] Using model at relative path: {modelPath}");
            }
            else if (File.Exists(fullModelPath))
            {
                actualModelPath = fullModelPath;
                _logger?.LogInformation($"[ImageNetClassifier] Using model at full path: {fullModelPath}");
            }
            else if (File.Exists(knownModelPath))
            {
                actualModelPath = knownModelPath;
                _logger?.LogInformation($"[ImageNetClassifier] ✓ Using model at known location: {knownModelPath}");
            }
            else
            {
                // Try alternative locations
                var altPaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src", "AiAgents.ContentModerationAgent.Web", "models", ModelFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "models", ModelFileName),
                    Path.Combine(_modelsDirectory, ModelFileName)
                };
                
                foreach (var altPath in altPaths)
                {
                    var altFullPath = Path.GetFullPath(altPath);
                    _logger?.LogInformation($"[ImageNetClassifier] Checking alternative: {altFullPath}");
                    if (File.Exists(altFullPath))
                    {
                        actualModelPath = altFullPath;
                        _logger?.LogInformation($"[ImageNetClassifier] Found model at: {altFullPath}");
                        break;
                    }
                }
            }
            
            if (actualModelPath != null)
            {
                _logger?.LogInformation($"[ImageNetClassifier] Model path found: {actualModelPath}");
                _logger?.LogInformation($"[ImageNetClassifier] Attempting to load model using direct ONNX Runtime...");
                
                // Try direct ONNX Runtime first (bypasses ML.NET pipeline issues)
                var onnxSession = GetOrCreateOnnxSession(actualModelPath);
                if (onnxSession != null)
                {
                    _logger?.LogInformation($"[ImageNetClassifier] ONNX Runtime session created successfully! Proceeding with classification...");
                    
                    // Use direct ONNX Runtime inference with proper pixel normalization
                    return await ClassifyWithDirectOnnxRuntimeAsync(imageBytes, onnxSession, cancellationToken);
                }
                
                // Fallback to ML.NET pipeline if direct ONNX Runtime fails
                _logger?.LogInformation($"[ImageNetClassifier] Direct ONNX Runtime failed, trying ML.NET pipeline...");
                var model = GetOrCreateModelWithPath(actualModelPath);
                if (model != null)
                {
                    _logger?.LogInformation($"[ImageNetClassifier] Model loaded successfully! Proceeding with classification...");
                }
                else
                {
                    _logger?.LogWarning($"[DogCatImageClassifier] Model loading returned null. Will use fallback.");
                }
                if (model != null)
                {
                    // Save image temporarily for ML.NET pipeline
                    var tempImagePath = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}.jpg");
                    try
                    {
                        await File.WriteAllBytesAsync(tempImagePath, imageBytes, cancellationToken);

                        // Create input data
                        var inputData = new[] { new { ImagePath = tempImagePath } };
                        var dataView = _mlContext.Data.LoadFromEnumerable(inputData);

                        // Transform using ONNX model
                        _logger?.LogInformation($"[ImageNetClassifier] Transforming image with ONNX model...");
                        var predictions = model.Transform(dataView);
                        _logger?.LogInformation($"[ImageNetClassifier] Transform completed. Getting predictions...");
                        
                        // Get predictions
                        var predictionEnumerable = _mlContext.Data.CreateEnumerable<OnnxOutput>(predictions, reuseRowObject: false);
                        var prediction = predictionEnumerable.FirstOrDefault();
                        _logger?.LogInformation($"[ImageNetClassifier] Prediction retrieved. Output is null: {prediction?.Output == null}");

                        if (prediction != null && prediction.Output != null)
                        {
                            // Find top prediction
                            var scores = prediction.Output;
                            var maxScore = scores.Max();
                            var maxIndex = Array.IndexOf(scores, maxScore);
                            _logger?.LogInformation($"[ImageNetClassifier] Max score: {maxScore}, Max index: {maxIndex}");

                            // Determine if it's a dog, cat, or other
                            string label;
                            bool isBlocked;
                            float confidence = maxScore;

                            if (IsDogClass(maxIndex))
                            {
                                label = "dog";
                                isBlocked = false; // Don't block directly, but boost text scores
                            }
                            else if (IsCatClass(maxIndex))
                            {
                                label = "cat";
                                isBlocked = false;
                            }
                            else
                            {
                                label = "other";
                                isBlocked = false;
                            }

                            _logger?.LogInformation($"[ImageNetClassifier] ✓ Classification result: Label={label}, Confidence={confidence:P2}, IsBlocked={isBlocked}, MaxIndex={maxIndex}");

                            var result = new ImageClassificationResult
                            {
                                Label = label,
                                Confidence = confidence,
                                IsBlocked = isBlocked,
                                Details = $"Detected: {label} (class {maxIndex}, confidence: {confidence:P2})"
                            };
                            
                            _logger?.LogInformation($"[ImageNetClassifier] Returning classification result: {result.Details}");
                            return result;
                        }
                        else
                        {
                            _logger?.LogWarning($"[DogCatImageClassifier] ONNX model prediction failed or returned no output. Prediction is null: {prediction == null}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // If ONNX inference fails, use fallback
                        _logger?.LogError(ex, $"[DogCatImageClassifier] ONNX inference error: {ex.Message}");
                    }
                    finally
                    {
                        // Clean up temp file
                        if (File.Exists(tempImagePath))
                        {
                            try { File.Delete(tempImagePath); } catch { }
                        }
                    }
                }
            }

            // Fallback: Simple heuristic if model not available
            return await Task.FromResult(new ImageClassificationResult
            {
                Label = "other",
                Confidence = 0.5f,
                IsBlocked = false,
                Details = "ONNX model not available. Using fallback - all images allowed. Please download resnet50-v2-7.onnx model."
            });
        }
        catch (Exception ex)
        {
            // If classification fails, default to "other" and allow
            return new ImageClassificationResult
            {
                Label = "other",
                Confidence = 0.0f,
                IsBlocked = false,
                Details = $"Classification error: {ex.Message}"
            };
        }
    }

    private InferenceSession? GetOrCreateOnnxSession(string modelPath)
    {
        if (_onnxSession != null)
            return _onnxSession;

        lock (_onnxSessionLock)
        {
            if (_onnxSession != null)
                return _onnxSession;

            var fullModelPath = Path.GetFullPath(modelPath);
            
            if (!File.Exists(modelPath) && !File.Exists(fullModelPath))
            {
                _logger?.LogWarning($"[DogCatImageClassifier] Model not found for ONNX Runtime: {modelPath} or {fullModelPath}");
                return null;
            }
            
            var actualModelPath = File.Exists(modelPath) ? modelPath : fullModelPath;
            _logger?.LogInformation($"[ImageNetClassifier] Creating ONNX Runtime session from: {Path.GetFullPath(actualModelPath)}");

            try
            {
                var options = new SessionOptions();
                _onnxSession = new InferenceSession(actualModelPath, options);
                _logger?.LogInformation($"[ImageNetClassifier] ✓ ONNX Runtime session created successfully!");
                return _onnxSession;
            }
            catch (TypeInitializationException tiex)
            {
                _logger?.LogError(tiex, $"[DogCatImageClassifier] TypeInitializationException creating ONNX Runtime session: {tiex.Message}");
                if (tiex.InnerException != null)
                {
                    _logger?.LogError($"[DogCatImageClassifier] Inner exception: {tiex.InnerException.Message}");
                }
                _logger?.LogError($"[DogCatImageClassifier] ONNX Runtime native DLLs may be missing. Check if onnxruntime.dll is available.");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"[DogCatImageClassifier] Error creating ONNX Runtime session: {ex.Message}");
                return null;
            }
        }
    }
    
    private async Task<ImageClassificationResult> ClassifyWithDirectOnnxRuntimeAsync(byte[] imageBytes, InferenceSession session, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger?.LogInformation($"[ImageNetClassifier] Preprocessing image for ONNX Runtime (direct method)...");
                
                // Load and resize image to 224x224
                using var ms = new MemoryStream(imageBytes);
                using var originalImage = new Bitmap(ms);
                
                // Resize to 224x224
                using var resizedImage = new Bitmap(originalImage, 224, 224);
                
                // Lock bitmap data
                var bitmapData = resizedImage.LockBits(
                    new Rectangle(0, 0, 224, 224),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);
                
                try
                {
                    // Create input tensor: [1, 3, 224, 224] - CHW format
                    var input = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
                    
                    unsafe
                    {
                        byte* ptr = (byte*)bitmapData.Scan0;
                        int stride = bitmapData.Stride;
                        
                        // Extract pixels and normalize to [0, 1] range
                        // ResNet50 expects BGR order (Blue, Green, Red) - but we'll try RGB first
                        for (int y = 0; y < 224; y++)
                        {
                            for (int x = 0; x < 224; x++)
                            {
                                int offset = y * stride + x * 3;
                                // Normalize to [0, 1] and store in CHW format
                                // ResNet50 expects BGR order (Blue, Green, Red)
                                // Bitmap Format24bppRgb stores as BGR: ptr[offset] = B, ptr[offset+1] = G, ptr[offset+2] = R
                                input[0, 0, y, x] = ptr[offset] / 255.0f;     // Blue channel -> channel 0
                                input[0, 1, y, x] = ptr[offset + 1] / 255.0f; // Green channel -> channel 1
                                input[0, 2, y, x] = ptr[offset + 2] / 255.0f; // Red channel -> channel 2
                            }
                        }
                    }
                    
                    _logger?.LogInformation($"[ImageNetClassifier] Image preprocessed. Running ONNX inference...");
                    
                    // Create input container - ResNet50 expects input named "data"
                    // Check what input name the model expects
                    var inputNames = session.InputMetadata.Keys;
                    _logger?.LogInformation($"[ImageNetClassifier] Model input names: {string.Join(", ", inputNames)}");
                    
                    var inputName = inputNames.FirstOrDefault() ?? "data";
                    _logger?.LogInformation($"[ImageNetClassifier] Using input name: {inputName}");
                    
                    var inputContainer = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(inputName, input)
                    };
                    
                    // Run inference
                    using var results = session.Run(inputContainer);
                    var output = results.First().Value as Tensor<float>;
                    
                    if (output != null)
                    {
                        var scores = output.ToArray();
                        
                        // Apply softmax normalization to convert logits to probabilities
                        var maxScore = scores.Max();
                        var expScores = scores.Select(s => Math.Exp(s - maxScore)).ToArray(); // Subtract max for numerical stability
                        var sumExp = expScores.Sum();
                        var probabilities = expScores.Select(e => (float)(e / sumExp)).ToArray();
                        
                        // Get top 5 predictions
                        var topPredictions = probabilities
                            .Select((prob, index) => new { Index = index, Probability = prob })
                            .OrderByDescending(x => x.Probability)
                            .Take(5)
                            .Select(x => new TopPrediction
                            {
                                Label = GetImageNetLabel(x.Index),
                                Confidence = x.Probability,
                                ClassIndex = x.Index
                            })
                            .ToList();
                        
                        var maxIndex = Array.IndexOf(probabilities, probabilities.Max());
                        var normalizedConfidence = probabilities[maxIndex];
                        
                        _logger?.LogInformation($"[ImageNetClassifier] Inference completed. Max score (raw): {scores[maxIndex]:F4}, Max index: {maxIndex}, Normalized confidence: {normalizedConfidence:P2}");
                        
                        // Get the actual ImageNet label (not just "other")
                        string label = GetImageNetLabel(maxIndex);
                        
                        // IMPORTANT: Check top 5 predictions for weapon-related labels
                        // ImageNet model sometimes misclassifies weapons (e.g., gun -> table lamp)
                        // If any of the top 5 predictions contains weapon keywords, prioritize it
                        var weaponKeywords = new[] { "gun", "pistol", "rifle", "assault rifle", "weapon", "firearm", "revolver" };
                        var weaponPrediction = topPredictions.FirstOrDefault(p => 
                            weaponKeywords.Any(keyword => p.Label.ToLowerInvariant().Contains(keyword.ToLowerInvariant())));
                        
                        if (weaponPrediction != null && weaponPrediction.Confidence > 0.05) // At least 5% confidence
                        {
                            label = weaponPrediction.Label;
                            normalizedConfidence = weaponPrediction.Confidence;
                            maxIndex = weaponPrediction.ClassIndex;
                            _logger?.LogInformation($"[ImageNetClassifier] ⚠️ Weapon detected in top 5 predictions! Using: {label} (confidence: {normalizedConfidence:P2}, was: {GetImageNetLabel(Array.IndexOf(probabilities, probabilities.Max()))})");
                        }
                        
                        bool isBlocked = false; // Dogs don't block directly, they boost text scores

                        // For moderation purposes, check if it's a dog (to boost text scores)
                        bool isDog = IsDogClass(maxIndex);
                        bool isCat = IsCatClass(maxIndex);
                        
                        _logger?.LogInformation($"[ImageNetClassifier] ✓ Classification result: Label={label}, Confidence={normalizedConfidence:P2}, IsBlocked={isBlocked}, MaxIndex={maxIndex}, IsDog={isDog}, IsCat={isCat}");

                        return new ImageClassificationResult
                        {
                            Label = label,
                            Confidence = normalizedConfidence,
                            IsBlocked = isBlocked,
                            Details = $"Detected: {label} (confidence: {normalizedConfidence:P2})"
                        };
                    }
                    else
                    {
                        _logger?.LogWarning($"[DogCatImageClassifier] ONNX Runtime output is null.");
                    }
                }
                finally
                {
                    resizedImage.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"[DogCatImageClassifier] Error in direct ONNX Runtime classification: {ex.Message}");
            }
            
            // Fallback
            return new ImageClassificationResult
            {
                Label = "other",
                Confidence = 0.5f,
                IsBlocked = false,
                Details = "ONNX Runtime classification failed. Using fallback."
            };
        }, cancellationToken);
    }

    private bool IsDogClass(int classIndex)
    {
        return DogClassIndices.Contains(classIndex);
    }

    private bool IsCatClass(int classIndex)
    {
        return CatClassIndices.Contains(classIndex);
    }
    
    private string GetImageNetLabel(int classIndex)
    {
        // Use ImageNetLabels to get the actual class name
        return ImageNetLabels.GetLabel(classIndex);
    }
}

// Output class for ONNX model predictions
internal class OnnxOutput
{
    [ColumnName("output")]
    public float[]? Output { get; set; }
}
