using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Materials;
using Mnemora.Domain.Materials;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Materials;

internal sealed class MaterialsRepository(
    MnemoraDbContext dbContext,
    ILogger<MaterialsRepository> logger)
    : IMaterialsRepository
{
    public void Add(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        dbContext.Materials.Add(material);
    }

    public void Remove(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        dbContext.Materials.Remove(material);
    }

    public async Task<Result<Material?, Error>>
        GetByIdAsync(
            MaterialId materialId,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(materialId);

        try
        {
            Material? material =
                await dbContext.Materials
                    .SingleOrDefaultAsync(
                        material =>
                            material.Id == materialId,
                        cancellationToken);

            return Result.Success<Material?, Error>(
                material);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "material.get.by.id.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить материал {MaterialId}",
                materialId.Value);

            return CommonErrors.Db(
                "material.get.by.id.failed",
                "Не удалось получить материал");
        }
    }

    public async Task<Result<IReadOnlyList<Question>, Error>>
        GetQuestionsByArticleIdAsync(
            MaterialId articleId,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(articleId);

        try
        {
            List<Question> questions =
                await dbContext.Materials
                    .OfType<Question>()
                    .Where(question =>
                        question.ArticleId == articleId)
                    .ToListAsync(cancellationToken);

            return Result.Success<
                IReadOnlyList<Question>,
                Error>(questions);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "material.questions.get.by.article.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить вопросы статьи {ArticleId}",
                articleId.Value);

            return CommonErrors.Db(
                "material.questions.get.by.article.failed",
                "Не удалось получить связанные вопросы статьи");
        }
    }
}