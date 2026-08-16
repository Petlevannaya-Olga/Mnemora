using CSharpFunctionalExtensions;
using Mnemora.Domain.Materials;
using Mnemora.Shared;

namespace Mnemora.Application.Materials.Content;

public interface IMaterialContentStore
{
    Task<UnitResult<Error>> CreateArticleAsync(
        MaterialId materialId,
        ArticleContent content,
        CancellationToken cancellationToken);

    Task<UnitResult<Error>> CreateQuestionAsync(
        MaterialId materialId,
        QuestionContent content,
        CancellationToken cancellationToken);

    UnitResult<Error> Delete(
        MaterialId materialId,
        MaterialType materialType);
}