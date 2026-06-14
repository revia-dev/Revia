using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;
using PrivateLibrary.Modules.Lending.Services;

namespace PrivateLibrary.Tests;

public class LendingServiceStatusTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly LendingService _service;

    public LendingServiceStatusTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new LendingService(_db, new FakeLendingPhotoStorage());
    }

    [Fact]
    public void CalculateStatus_NoReturnDate_NoDueDate_ReturnsActive()
    {
        var entry = new LendingEntry { LoanDate = DateTime.Today, LenderId = "a", BorrowerId = "b" };
        Assert.Equal(LendingEntryStatus.Active, _service.CalculateStatus(entry));
    }

    [Fact]
    public void CalculateStatus_ReturnedAt_ReturnsReturned()
    {
        var entry = new LendingEntry
        {
            LoanDate = DateTime.Today.AddDays(-5),
            ReturnedAt = DateTime.Today,
            LenderId = "a",
            BorrowerId = "b"
        };
        Assert.Equal(LendingEntryStatus.Returned, _service.CalculateStatus(entry));
    }

    [Fact]
    public void CalculateStatus_DueDateInPast_ReturnsOverdue()
    {
        var entry = new LendingEntry
        {
            LoanDate = DateTime.Today.AddDays(-10),
            DueDate = DateTime.Today.AddDays(-1),
            LenderId = "a",
            BorrowerId = "b"
        };
        Assert.Equal(LendingEntryStatus.Overdue, _service.CalculateStatus(entry));
    }

    [Fact]
    public void CalculateStatus_DueDateToday_ReturnsActive()
    {
        var entry = new LendingEntry
        {
            LoanDate = DateTime.Today.AddDays(-5),
            DueDate = DateTime.Today,
            LenderId = "a",
            BorrowerId = "b"
        };
        Assert.Equal(LendingEntryStatus.Active, _service.CalculateStatus(entry));
    }

    [Fact]
    public void CalculateStatus_StatusLost_ReturnsLost()
    {
        var entry = new LendingEntry
        {
            LoanDate = DateTime.Today,
            Status = LendingEntryStatus.Lost,
            LenderId = "a",
            BorrowerId = "b"
        };
        Assert.Equal(LendingEntryStatus.Lost, _service.CalculateStatus(entry));
    }

    [Fact]
    public void CalculateStatus_StatusGivenAway_ReturnsGivenAway()
    {
        var entry = new LendingEntry
        {
            LoanDate = DateTime.Today,
            Status = LendingEntryStatus.GivenAway,
            LenderId = "a",
            BorrowerId = "b"
        };
        Assert.Equal(LendingEntryStatus.GivenAway, _service.CalculateStatus(entry));
    }

    [Fact]
    public void CalculateStatus_ReturnedAt_OverridesDueDate()
    {
        var entry = new LendingEntry
        {
            LoanDate = DateTime.Today.AddDays(-10),
            DueDate = DateTime.Today.AddDays(-5),
            ReturnedAt = DateTime.Today,
            LenderId = "a",
            BorrowerId = "b"
        };
        Assert.Equal(LendingEntryStatus.Returned, _service.CalculateStatus(entry));
    }

    public void Dispose() => _db.Dispose();
}
