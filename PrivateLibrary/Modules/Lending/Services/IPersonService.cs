using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Modules.Lending.Services;

public interface IPersonService
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Person>> GetLookupAsync(CancellationToken cancellationToken = default);
    Task<Person> CreateAsync(Person person, CancellationToken cancellationToken = default);
    Task<Person?> UpdateAsync(Person person, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
