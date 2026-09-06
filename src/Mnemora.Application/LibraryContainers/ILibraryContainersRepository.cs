using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;

namespace Mnemora.Application.LibraryContainers;

public interface ILibraryContainersRepository
{
    void Add(LibraryContainer container);

    void Remove(LibraryContainer container);

    Task<Result<LibraryContainer?, Error>> GetByIdAsync(
        LibraryContainerId containerId,
        CancellationToken cancellationToken);

    Task<Result<LibraryContainer?, Error>> GetRootBySectionIdAsync(
        SectionId sectionId,
        CancellationToken cancellationToken);

    Task<Result<bool, Error>> ExistsAsync(
        Expression<Func<LibraryContainer, bool>> predicate,
        CancellationToken cancellationToken);
}
