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

    public DbSet<LendingEntry> LendingEntries => Set<LendingEntry>();
    public DbSet<LendingItem> LendingItems => Set<LendingItem>();
    public DbSet<LendingCategory> LendingCategories => Set<LendingCategory>();
    public DbSet<LendingEntryCategory> LendingEntryCategories => Set<LendingEntryCategory>();
    public DbSet<LendingPhoto> LendingPhotos => Set<LendingPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.Property(x => x.IsSystemUser)
                .HasDefaultValue(false);
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
