using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Modules.Lending.Services;

public sealed class PersonService : IPersonService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PersonService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.People
            .AsNoTracking()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.People.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.People
            .AsNoTracking()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new Person
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Nickname = x.Nickname
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Person> CreateAsync(Person person, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        person.CreatedAt = DateTime.UtcNow;
        person.UpdatedAt = DateTime.UtcNow;

        db.People.Add(person);
        await db.SaveChangesAsync(cancellationToken);
        return person;
    }

    public async Task<Person?> UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.People.FirstOrDefaultAsync(x => x.Id == person.Id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.FirstName = person.FirstName;
        existing.LastName = person.LastName;
        existing.Nickname = person.Nickname;
        existing.Email = person.Email;
        existing.Phone = person.Phone;
        existing.Street = person.Street;
        existing.HouseNumber = person.HouseNumber;
        existing.PostalCode = person.PostalCode;
        existing.City = person.City;
        existing.Notes = person.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var hasReferences = await db.LendingEntries.AnyAsync(x => x.LenderId == id || x.BorrowerId == id, cancellationToken);
        if (hasReferences)
        {
            return false;
        }

        var person = await db.People.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (person is null)
        {
            return false;
        }

        db.People.Remove(person);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
