using AiAgents.ContentModerationAgent.Application.Runners;
using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using AiAgents.ContentModerationAgent.Web.BackgroundServices;
using AiAgents.ContentModerationAgent.Web.Hubs;
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
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ITrainingService, TrainingService>();
builder.Services.AddScoped<IThresholdService, ThresholdService>();
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<IWordlistService, WordlistService>();

// ML
builder.Services.AddSingleton<IContentClassifier>(sp => 
    new MlNetContentClassifier("models", sp));

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
