using CSharpFunctionalExtensions;
using Mnemora.Domain.Materials;
using Mnemora.Shared;

namespace Mnemora.Application.Materials;

public interface IMaterialsRepository
{
    void Add(Material material);

    void Remove(Material material);

    Task<Result<Material?, Error>> GetByIdAsync(
        MaterialId materialId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Question>, Error>>
        GetQuestionsByArticleIdAsync(
            MaterialId articleId,
            CancellationToken cancellationToken);
}