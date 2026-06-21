using System.ComponentModel.DataAnnotations;
using Revia.Modules.Lending.Models;

namespace Revia.Tests;

public class LendingEntryEditModelValidationTests
{
    private static IList<ValidationResult> Validate(LendingEntryEditModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_Model_NoErrors()
    {
        var model = new LendingEntryEditModel
        {
            LenderId = "lender",
            BorrowerId = "borrower",
            LoanDate = DateTime.Today,
            ItemNames = ["Buch"]
        };
        Assert.Empty(Validate(model));
    }

    [Fact]
    public void DueDate_BeforeLoanDate_ProducesError()
    {
        var model = new LendingEntryEditModel
        {
            LenderId = "lender",
            BorrowerId = "borrower",
            LoanDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(-1),
            ItemNames = ["Buch"]
        };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LendingEntryEditModel.DueDate)));
    }

    [Fact]
    public void SameLenderAndBorrower_ProducesError()
    {
        var model = new LendingEntryEditModel
        {
            LenderId = "same",
            BorrowerId = "same",
            LoanDate = DateTime.Today,
            ItemNames = ["Buch"]
        };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LendingEntryEditModel.LenderId)));
    }

    [Fact]
    public void EmptyItemNames_ProducesError()
    {
        var model = new LendingEntryEditModel
        {
            LenderId = "lender",
            BorrowerId = "borrower",
            LoanDate = DateTime.Today,
            ItemNames = []
        };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LendingEntryEditModel.ItemNames)));
    }

    [Fact]
    public void WhitespaceOnlyItemNames_ProducesError()
    {
        var model = new LendingEntryEditModel
        {
            LenderId = "lender",
            BorrowerId = "borrower",
            LoanDate = DateTime.Today,
            ItemNames = ["   ", ""]
        };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LendingEntryEditModel.ItemNames)));
    }

    [Fact]
    public void ReturnedAt_BeforeLoanDate_ProducesError()
    {
        var model = new LendingEntryEditModel
        {
            LenderId = "lender",
            BorrowerId = "borrower",
            LoanDate = DateTime.Today,
            ReturnedAt = DateTime.Today.AddDays(-1),
            ItemNames = ["Buch"]
        };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LendingEntryEditModel.ReturnedAt)));
    }
}
