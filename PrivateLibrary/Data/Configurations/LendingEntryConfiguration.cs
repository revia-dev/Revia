using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Data.Configurations;

public sealed class LendingEntryConfiguration : IEntityTypeConfiguration<LendingEntry>
{
    public void Configure(EntityTypeBuilder<LendingEntry> builder)
    {
        builder.ToTable("LendingEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ItemName).HasMaxLength(200);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.Property(x => x.LoanDate).IsRequired();
        builder.Property(x => x.DueDate);
        builder.Property(x => x.ReturnedAt);
        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.Lender)
            .WithMany(x => x.LentEntries)
            .HasForeignKey(x => x.LenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Borrower)
            .WithMany(x => x.BorrowedEntries)
            .HasForeignKey(x => x.BorrowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.LendingEntry)
            .HasForeignKey(x => x.LendingEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.CategoryLinks)
            .WithOne(x => x.LendingEntry)
            .HasForeignKey(x => x.LendingEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Photos)
            .WithOne(x => x.LendingEntry)
            .HasForeignKey(x => x.LendingEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.LoanDate);
        builder.HasIndex(x => x.DueDate);
        builder.HasIndex(x => new { x.LenderId, x.BorrowerId });
    }
}

public sealed class LendingItemConfiguration : IEntityTypeConfiguration<LendingItem>
{
    public void Configure(EntityTypeBuilder<LendingItem> builder)
    {
        builder.ToTable("LendingItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.HasIndex(x => x.LendingEntryId);
        builder.HasIndex(x => new { x.LendingEntryId, x.SortOrder });
    }
}

public sealed class LendingPhotoConfiguration : IEntityTypeConfiguration<LendingPhoto>
{
    public void Configure(EntityTypeBuilder<LendingPhoto> builder)
    {
        builder.ToTable("LendingPhotos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.OriginalName)
            .HasMaxLength(260);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.LendingEntryId);
        builder.HasIndex(x => new { x.LendingEntryId, x.SortOrder });
    }
}

public sealed class LendingCategoryConfiguration : IEntityTypeConfiguration<LendingCategory>
{
    public void Configure(EntityTypeBuilder<LendingCategory> builder)
    {
        builder.ToTable("LendingCategories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}

public sealed class LendingEntryCategoryConfiguration : IEntityTypeConfiguration<LendingEntryCategory>
{
    public void Configure(EntityTypeBuilder<LendingEntryCategory> builder)
    {
        builder.ToTable("LendingEntryCategories");

        builder.HasKey(x => new { x.LendingEntryId, x.LendingCategoryId });

        builder.HasOne(x => x.LendingEntry)
            .WithMany(x => x.CategoryLinks)
            .HasForeignKey(x => x.LendingEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LendingCategory)
            .WithMany(x => x.EntryLinks)
            .HasForeignKey(x => x.LendingCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.LendingCategoryId);
    }
}
