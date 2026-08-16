using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.GetDetails;

public sealed class GetMaterialDetailsQueryHandler(
    IReadDbContext readDbContext,
    IMaterialContentStore materialContentStore,
    ILogger<GetMaterialDetailsQueryHandler> logger)
    : IQueryHandler<MaterialDetailsDto, GetMaterialDetailsQuery>
{
    public async Task<Result<MaterialDetailsDto, Errors>> Handle(
        GetMaterialDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        var materialIdResult = MaterialId.Create(query.MaterialId);

        if (materialIdResult.IsFailure)
        {
            return materialIdResult.Error.ToErrors();
        }

        try
        {
            var material = await readDbContext.MaterialsRead
                .SingleOrDefaultAsync(
                    material => material.Id == materialIdResult.Value,
                    cancellationToken);

            if (material is null)
            {
                return new Error(
                    "material.not.found",
                    "Материал не найден",
                    ErrorType.NOT_FOUND,
                    nameof(query.MaterialId)).ToErrors();
            }

            var topic = await readDbContext.TopicsRead
                .SingleOrDefaultAsync(
                    topic => topic.Id == material.TopicId,
                    cancellationToken);

            if (topic is null)
            {
                return CommonErrors.Failure(
                    "material.topic.not.found",
                    "Не удалось определить тему материала").ToErrors();
            }

            var section = await readDbContext.SectionsRead
                .SingleOrDefaultAsync(
                    section => section.Id == topic.SectionId,
                    cancellationToken);

            if (section is null)
            {
                return CommonErrors.Failure(
                    "material.section.not.found",
                    "Не удалось определить раздел материала").ToErrors();
            }

            var metadata = new MaterialMetadataDto(
                material.Id.Value,
                topic.Id.Value,
                topic.Name.Value,
                section.Id.Value,
                section.Name.Value,
                material.Title.Value,
                material.Type.ToString(),
                material.Difficulty.ToString(),
                material.Icon.Key,
                material.ExperienceRewards.StudyPoints,
                material.ExperienceRewards.ReviewPoints,
                material.LearningRevision,
                material.Tags
                    .OrderBy(tag => tag.Value, StringComparer.OrdinalIgnoreCase)
                    .Select(tag => tag.Value)
                    .ToArray(),
                material.CreatedAt,
                material.UpdatedAt);

            return material switch
            {
                Article article => await CreateArticleDetailsAsync(
                    article,
                    metadata,
                    cancellationToken),

                Question question => await CreateQuestionDetailsAsync(
                    question,
                    metadata,
                    cancellationToken),

                _ => CommonErrors.Failure(
                    "material.type.is.invalid",
                    "Обнаружен материал неизвестного типа").ToErrors()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение материала {MaterialId} было отменено",
                query.MaterialId);

            return CommonErrors.OperationCancelled(
                "material.get.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить материал {MaterialId}",
                query.MaterialId);

            return CommonErrors.Db(
                "material.get.failed",
                "Не удалось загрузить материал").ToErrors();
        }
    }

    private async Task<Result<MaterialDetailsDto, Errors>> CreateArticleDetailsAsync(
        Article article,
        MaterialMetadataDto metadata,
        CancellationToken cancellationToken)
    {
        var contentResult = await materialContentStore.ReadArticleAsync(
            article.Id,
            cancellationToken);

        if (contentResult.IsFailure)
        {
            return contentResult.Error.ToErrors();
        }

        return Result.Success<MaterialDetailsDto, Errors>(
            new ArticleDetailsDto(metadata, contentResult.Value.BodyMarkdown));
    }

    private async Task<Result<MaterialDetailsDto, Errors>> CreateQuestionDetailsAsync(
        Question question,
        MaterialMetadataDto metadata,
        CancellationToken cancellationToken)
    {
        var contentResult = await materialContentStore.ReadQuestionAsync(
            question.Id,
            cancellationToken);

        if (contentResult.IsFailure)
        {
            return contentResult.Error.ToErrors();
        }

        RelatedArticleDto? relatedArticle = null;

        if (question.ArticleId is not null)
        {
            var articleId = question.ArticleId;

            var article = await readDbContext.MaterialsRead
                .OfType<Article>()
                .SingleOrDefaultAsync(
                    article => article.Id == articleId,
                    cancellationToken);

            if (article is null)
            {
                return CommonErrors.Failure(
                    "question.related.article.not.found",
                    "Не удалось найти связанную с вопросом статью").ToErrors();
            }

            relatedArticle = new RelatedArticleDto(
                article.Id.Value,
                article.Title.Value);
        }

        return Result.Success<MaterialDetailsDto, Errors>(
            new QuestionDetailsDto(
                metadata,
                relatedArticle,
                contentResult.Value.PromptMarkdown,
                contentResult.Value.ReferenceAnswerMarkdown));
    }
}