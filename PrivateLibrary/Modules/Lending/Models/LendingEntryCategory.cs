namespace PrivateLibrary.Modules.Lending.Models;

public sealed class LendingEntryCategory
{
    public int LendingEntryId { get; set; }
    public LendingEntry? LendingEntry { get; set; }

    public int LendingCategoryId { get; set; }
    public LendingCategory? LendingCategory { get; set; }
}
