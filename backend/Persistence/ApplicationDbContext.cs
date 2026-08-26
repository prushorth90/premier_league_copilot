using Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<LocalProfile> Profiles => Set<LocalProfile>();

    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    public DbSet<RecommendationSnapshot> RecommendationSnapshots => Set<RecommendationSnapshot>();

    public DbSet<RecommendationHistoryEntry> RecommendationHistory => Set<RecommendationHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalProfile>(entity =>
        {
            entity.ToTable("local_profiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.DisplayName).HasMaxLength(120).IsRequired();
            entity.HasIndex(profile => profile.SelectedFplTeamId);
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_local_profiles_selected_fpl_team_id",
                "\"SelectedFplTeamId\" IS NULL OR \"SelectedFplTeamId\" > 0"));
        });

        modelBuilder.Entity<ApplicationSetting>(entity =>
        {
            entity.ToTable("application_settings");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Key).HasMaxLength(100).IsRequired();
            entity.Property(setting => setting.ValueJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(setting => new { setting.ProfileId, setting.Key }).IsUnique();
            entity.HasOne(setting => setting.Profile)
                .WithMany(profile => profile.Settings)
                .HasForeignKey(setting => setting.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecommendationSnapshot>(entity =>
        {
            entity.ToTable("recommendation_snapshots");
            entity.HasKey(snapshot => snapshot.Id);
            entity.Property(snapshot => snapshot.Kind).HasMaxLength(40).IsRequired();
            entity.Property(snapshot => snapshot.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(snapshot => new { snapshot.FplTeamId, snapshot.Kind }).IsUnique();
            entity.HasIndex(snapshot => snapshot.ExpiresAt);
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_recommendation_snapshots_fpl_team_id",
                "\"FplTeamId\" > 0"));
        });

        modelBuilder.Entity<RecommendationHistoryEntry>(entity =>
        {
            entity.ToTable("recommendation_history");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Kind).HasMaxLength(40).IsRequired();
            entity.Property(entry => entry.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(entry => new { entry.FplTeamId, entry.Kind, entry.RecordedAt });
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_recommendation_history_fpl_team_id",
                "\"FplTeamId\" > 0"));
        });
    }
}