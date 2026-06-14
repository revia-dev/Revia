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
    public DbSet<Movie> Movies => Set<Movie>();
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
