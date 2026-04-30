using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Features.Lending.Models;

namespace PrivateLibrary.Features.Lending.Services;

public sealed class PersonService : IPersonService
{
    private readonly AppDbContext _db;

    public PersonService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.People
            .AsNoTracking()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.People.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _db.People
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
        person.CreatedAt = DateTime.UtcNow;
        person.UpdatedAt = DateTime.UtcNow;

        _db.People.Add(person);
        await _db.SaveChangesAsync(cancellationToken);
        return person;
    }

    public async Task<Person?> UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        var existing = await _db.People.FirstOrDefaultAsync(x => x.Id == person.Id, cancellationToken);
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

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var hasReferences = await _db.LendingEntries.AnyAsync(x => x.LenderId == id || x.BorrowerId == id, cancellationToken);
        if (hasReferences)
        {
            return false;
        }

        var person = await _db.People.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (person is null)
        {
            return false;
        }

        _db.People.Remove(person);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
