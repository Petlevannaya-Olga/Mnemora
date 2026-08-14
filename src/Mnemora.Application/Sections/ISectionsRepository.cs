using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Mnemora.Domain.Sections;
using Mnemora.Shared;

namespace Mnemora.Application.Sections;

public interface ISectionsRepository
{
    void Add(Section section);

    Task<Result<bool, Error>> ExistsAsync(
        Expression<Func<Section, bool>> predicate,
        CancellationToken cancellationToken);
}