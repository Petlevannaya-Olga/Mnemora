using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Mnemora.Domain.Topics;
using Mnemora.Shared;

namespace Mnemora.Application.Topics;

public interface ITopicsRepository
{
    void Add(Topic topic);

    Task<Result<bool, Error>> ExistsAsync(
        Expression<Func<Topic, bool>> predicate,
        CancellationToken cancellationToken);
}