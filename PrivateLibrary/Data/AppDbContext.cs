using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdLink> HouseholdLinks => Set<HouseholdLink>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<TvProgram> TvPrograms => Set<TvProgram>();
    public DbSet<TvProgramEntry> TvProgramEntries => Set<TvProgramEntry>();
    public DbSet<BmiRecord> BmiRecords => Set<BmiRecord>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameExpansion> GameExpansions => Set<GameExpansion>();
    public DbSet<GameMatch> GameMatches => Set<GameMatch>();
    public DbSet<GameMatchPhoto> GameMatchPhotos => Set<GameMatchPhoto>();
    public DbSet<GameMatchParticipant> GameMatchParticipants => Set<GameMatchParticipant>();
    public DbSet<GameTypeAssignment> GameTypeAssignments => Set<GameTypeAssignment>();
    public DbSet<GameScoreDefinition> GameScoreDefinitions => Set<GameScoreDefinition>();
    public DbSet<GameExpansionFile> GameExpansionFiles => Set<GameExpansionFile>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<LendingEntry> LendingEntries => Set<LendingEntry>();
    public DbSet<LendingItem> LendingItems => Set<LendingItem>();
    public DbSet<LendingCategory> LendingCategories => Set<LendingCategory>();
    public DbSet<LendingEntryCategory> LendingEntryCategories => Set<LendingEntryCategory>();
    public DbSet<LendingPhoto> LendingPhotos => Set<LendingPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Household>(builder =>
        {
            builder.HasIndex(x => x.InviteCode).IsUnique();
        });

        modelBuilder.Entity<HouseholdLink>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdAId, x.HouseholdBId }).IsUnique();
        });

        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.Property(x => x.IsSystemUser)
                .HasDefaultValue(false);

            builder.Property(x => x.IsLoginless)
                .HasDefaultValue(false);

            builder.HasOne(x => x.Household)
                .WithMany(h => h.Members)
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Category names are unique per household, not globally.
        modelBuilder.Entity<LendingCategory>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<LendingEntry>(builder =>
        {
            builder.HasIndex(x => x.HouseholdId);
        });

        modelBuilder.Entity<Movie>(builder =>
        {
            builder.HasIndex(x => x.HouseholdId);
            builder.HasIndex(x => new { x.HouseholdId, x.Titel });
        });

        modelBuilder.Entity<TvProgram>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdId, x.OwnerUserId });

            builder.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BmiRecord>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdId, x.PersonId, x.MeasuredOn });

            builder.HasOne(x => x.Person)
                .WithMany()
                .HasForeignKey(x => x.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Game>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdId, x.Name });
        });

        modelBuilder.Entity<Wishlist>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdId, x.OwnerUserId });

            builder.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WishlistItem>(builder =>
        {
            builder.HasOne(x => x.Wishlist)
                .WithMany(w => w.Items)
                .HasForeignKey(x => x.WishlistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameTypeAssignment>(builder =>
        {
            builder.HasIndex(x => new { x.GameId, x.Type }).IsUnique();
            builder.HasOne(x => x.Game)
                .WithMany(g => g.Types)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameScoreDefinition>(builder =>
        {
            builder.HasOne(x => x.Game)
                .WithMany(g => g.ScoreDefinitions)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameExpansion>(builder =>
        {
            builder.HasOne(x => x.Game)
                .WithMany(g => g.Expansions)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameExpansionFile>(builder =>
        {
            builder.HasOne(x => x.GameExpansion)
                .WithMany(e => e.Files)
                .HasForeignKey(x => x.GameExpansionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameMatch>(builder =>
        {
            builder.HasIndex(x => new { x.HouseholdId, x.StartedAt });

            builder.HasOne(x => x.Game)
                .WithMany(g => g.Matches)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ScoreDefinition)
                .WithMany()
                .HasForeignKey(x => x.ScoreDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GameMatchPhoto>(builder =>
        {
            builder.HasOne(x => x.GameMatch)
                .WithMany(m => m.Photos)
                .HasForeignKey(x => x.GameMatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameMatchParticipant>(builder =>
        {
            builder.HasIndex(x => new { x.GameMatchId, x.PersonId }).IsUnique();

            builder.HasOne(x => x.GameMatch)
                .WithMany(m => m.Participants)
                .HasForeignKey(x => x.GameMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Person)
                .WithMany()
                .HasForeignKey(x => x.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TvProgramEntry>(builder =>
        {
            builder.HasIndex(x => x.TvProgramId);

            builder.HasOne(x => x.TvProgram)
                .WithMany(p => p.Entries)
                .HasForeignKey(x => x.TvProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Movie)
                .WithMany()
                .HasForeignKey(x => x.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityRole>(builder =>
        {
            builder.Property(x => x.Name).HasMaxLength(256);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is LendingEntry lendingEntry)
            {
                lendingEntry.UpdatedAt = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                {
                    lendingEntry.CreatedAt = DateTime.UtcNow;
                }
            }

            if (entry.Entity is LendingCategory lendingCategory)
            {
                lendingCategory.UpdatedAt = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                {
                    lendingCategory.CreatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
