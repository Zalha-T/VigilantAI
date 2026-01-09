using AiAgents.ContentModerationAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Infrastructure;

public class ContentModerationDbContext : DbContext
{
    public ContentModerationDbContext(DbContextOptions<ContentModerationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Content> Contents { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<Prediction> Predictions { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ModelVersion> ModelVersions { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    public DbSet<Context> Contexts { get; set; }
    public DbSet<BlockedWord> BlockedWords { get; set; }
    public DbSet<ContentImage> ContentImages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Content configuration
        modelBuilder.Entity<Content>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AuthorId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Author configuration
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username);
        });

        // Prediction configuration
        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Decision).HasConversion<int>();
            entity.Property(e => e.Confidence).HasConversion<int>();
            entity.HasIndex(e => e.ContentId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Review configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GoldLabel).HasConversion<int>();
            entity.HasIndex(e => e.ContentId);
            entity.HasIndex(e => e.GoldLabel);
        });

        // ModelVersion configuration
        modelBuilder.Entity<ModelVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Version);
        });

        // SystemSettings configuration
        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // Context configuration
        modelBuilder.Entity<Context>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ContentId);
        });

        // BlockedWord configuration
        modelBuilder.Entity<BlockedWord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Word).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Word);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
        });

        // ContentImage configuration
        modelBuilder.Entity<ContentImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ClassificationResult).HasMaxLength(1000);
            entity.HasIndex(e => e.ContentId);
            entity.HasIndex(e => e.CreatedAt);
            
            // Relationship with Content (one-to-one)
            entity.HasOne(e => e.Content)
                .WithOne(c => c.Image)
                .HasForeignKey<ContentImage>(e => e.ContentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
