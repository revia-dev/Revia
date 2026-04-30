using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();
    public DbSet<LendingEntry> LendingEntries => Set<LendingEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
            if (entry.Entity is Person person)
            {
                person.UpdatedAt = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                {
                    person.CreatedAt = DateTime.UtcNow;
                }
            }

            if (entry.Entity is LendingEntry lendingEntry)
            {
                lendingEntry.UpdatedAt = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                {
                    lendingEntry.CreatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
