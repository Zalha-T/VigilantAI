using AiAgents.ContentModerationAgent.Application.Runners;
using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using AiAgents.ContentModerationAgent.Web.BackgroundServices;
using AiAgents.ContentModerationAgent.Web.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Content Moderation Agent API",
        Version = "v1",
        Description = "API for Content Moderation Agent - Automatska moderacija sadržaja sa AI agentom"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Database
builder.Services.AddDbContext<ContentModerationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=ContentModerationDb;Trusted_Connection=True;"));

// Application Services
builder.Services.AddScoped<IQueueService, QueueService>();
  builder.Services.AddScoped<IScoringService>(sp => 
      new ScoringService(
          sp.GetRequiredService<ContentModerationDbContext>(),
          sp.GetRequiredService<IContentClassifier>(),
          sp.GetRequiredService<IContextService>(),
          sp.GetRequiredService<IThresholdService>(),
          sp.GetService<IImageClassifier>(),
          sp.GetService<IWordlistService>())); // Inject IWordlistService for image label checking
builder.Services.AddScoped<IReviewService>(sp => 
    new ReviewService(
        sp.GetRequiredService<ContentModerationDbContext>(),
        sp.GetRequiredService<ITrainingService>(),
        sp.GetRequiredService<ILogger<ReviewService>>())); // Inject ITrainingService for immediate retraining
builder.Services.AddScoped<ITrainingService>(sp => 
    new TrainingService(
        sp.GetRequiredService<ContentModerationDbContext>(),
        sp.GetRequiredService<IContentClassifier>(),
        sp.GetRequiredService<IThresholdService>(),
        sp.GetRequiredService<ILogger<TrainingService>>()));
builder.Services.AddScoped<IThresholdService, ThresholdService>();
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<IWordlistService, WordlistService>();
builder.Services.AddScoped<IImageStorageService>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var basePath = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "images");
    return new ImageStorageService(basePath);
});

// ML
builder.Services.AddSingleton<IContentClassifier>(sp => 
    new MlNetContentClassifier("models", sp));
builder.Services.AddSingleton<IImageClassifier>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var logger = sp.GetRequiredService<ILogger<ImageNetClassifier>>();
    var programLogger = sp.GetRequiredService<ILogger<Program>>();
    
    // ContentRootPath in ASP.NET Core points to bin/Debug/net8.0/ during runtime
    // We need to go up to the project root where .csproj is located
    // Project root: backend/src/AiAgents.ContentModerationAgent.Web/
    // Models should be in: backend/src/AiAgents.ContentModerationAgent.Web/models/
    
    var contentRoot = env.ContentRootPath;
    programLogger.LogInformation("========== INITIALIZING IMAGE CLASSIFIER ==========");
    programLogger.LogInformation($"[Program] ContentRootPath (runtime): {contentRoot}");
    
    // If ContentRootPath contains "bin", go up to project root
    string projectRoot;
    if (contentRoot.Contains("bin"))
    {
        var binIndex = contentRoot.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
        projectRoot = contentRoot.Substring(0, binIndex).TrimEnd('\\', '/');
    }
    else
    {
        // Already at project root
        projectRoot = contentRoot;
    }
    
    var modelsDir = Path.Combine(projectRoot, "models");
    var fullModelsPath = Path.GetFullPath(modelsDir);
    
    programLogger.LogInformation($"[Program] Project root: {projectRoot}");
    programLogger.LogInformation($"[Program] Models directory: {fullModelsPath}");
    programLogger.LogInformation($"[Program] Models directory exists: {Directory.Exists(fullModelsPath)}");
    
    if (Directory.Exists(fullModelsPath))
    {
        var modelFile = Path.Combine(fullModelsPath, "resnet50-v2-7.onnx");
        var modelExists = System.IO.File.Exists(modelFile);
        programLogger.LogInformation($"[Program] Model file path: {modelFile}");
        programLogger.LogInformation($"[Program] Model file exists: {modelExists}");
        if (modelExists)
        {
            var fileInfo = new System.IO.FileInfo(modelFile);
            programLogger.LogInformation($"[Program] Model file size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
        }
    }
    
    // Use calculated path
    var finalModelsPath = fullModelsPath;
    
    programLogger.LogInformation($"[Program] Final models path: {finalModelsPath}");
    programLogger.LogInformation("========== IMAGE CLASSIFIER INITIALIZED ==========");
    
    return new ImageNetClassifier(finalModelsPath, logger);
});

// Runners
builder.Services.AddScoped<ModerationAgentRunner>();
builder.Services.AddScoped<RetrainAgentRunner>();
builder.Services.AddScoped<ThresholdUpdateRunner>();

// Background Services
builder.Services.AddHostedService<ModerationAgentBackgroundService>();
builder.Services.AddHostedService<RetrainAgentBackgroundService>();
builder.Services.AddHostedService<ThresholdUpdateAgentBackgroundService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Content Moderation Agent API v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
    });
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ModerationHub>("/moderationHub");

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContentModerationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Ensure database exists and all tables are created
        // EnsureCreated will create the database and all tables if they don't exist
        // Note: If database already exists, it won't modify existing tables
        // For new tables (like BlockedWords), you may need to delete and recreate the database
        var created = await db.Database.EnsureCreatedAsync();
        if (created)
        {
            logger.LogInformation("Database created successfully");
        }
        else
        {
            logger.LogInformation("Database already exists");
        }
        
        // Load active ML model if available
        var classifier = scope.ServiceProvider.GetRequiredService<IContentClassifier>();
        if (classifier is MlNetContentClassifier mlClassifier)
        {
            try
            {
                var activeModel = await db.ModelVersions
                    .Where(m => m.IsActive)
                    .OrderByDescending(m => m.Version)
                    .FirstOrDefaultAsync();
                
                if (activeModel != null && !string.IsNullOrEmpty(activeModel.ModelPath))
                {
                    // Use same path logic as TrainingService
                    var baseDirectory = AppContext.BaseDirectory;
                    var modelsDirectory = Path.Combine(baseDirectory, "models");
                    
                    if (baseDirectory.Contains("bin"))
                    {
                        var binIndex = baseDirectory.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
                        var projectRoot = baseDirectory.Substring(0, binIndex).TrimEnd('\\', '/');
                        modelsDirectory = Path.Combine(projectRoot, "models");
                    }
                    
                    var modelPath = Path.GetFullPath(Path.Combine(modelsDirectory, $"model_v{activeModel.Version}.zip"));
                    
                    if (File.Exists(modelPath))
                    {
                        await mlClassifier.LoadModelAsync(modelPath);
                        logger.LogInformation("✓ Active ML model v{Version} loaded from: {ModelPath}", 
                            activeModel.Version, modelPath);
                    }
                    else
                    {
                        logger.LogWarning("Active model v{Version} not found at: {ModelPath}. Model is in database but file is missing.", 
                            activeModel.Version, modelPath);
                        logger.LogInformation("💡 Tip: Use POST /api/model/save-active-model to save the model if it's loaded in memory.");
                    }
                }
                else
                {
                    logger.LogInformation("No active ML model found. Using wordlist-based heuristics only.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load active ML model. Using wordlist-based heuristics only.");
            }
        }
        
        // Seed initial data
        var seeder = new DatabaseSeeder(db);
        await seeder.SeedAsync();
        logger.LogInformation("Database seeding completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error ensuring database is created");
        // Don't fail startup, but log the error
    }
}

app.Run();
