using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Learning.GetOptions;

public sealed class GetMaterialLearningLinkOptionsQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetMaterialLearningLinkOptionsQueryHandler> logger)
    : IQueryHandler<
        MaterialLearningLinkOptionsDto,
        GetMaterialLearningLinkOptionsQuery>
{
    public async Task<Result<MaterialLearningLinkOptionsDto, Errors>> Handle(
        GetMaterialLearningLinkOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var topicIdResult = TopicId.Create(query.TopicId);

        if (topicIdResult.IsFailure)
        {
            return topicIdResult.Error.ToErrors();
        }

        TopicId topicId = topicIdResult.Value;

        try
        {
            bool topicExists = await readDbContext.TopicsRead
                .AnyAsync(
                    topic => topic.Id == topicId,
                    cancellationToken);

            if (!topicExists)
            {
                return CommonErrors.NotFound(
                        "topic.not.found",
                        $"Тема с идентификатором '{query.TopicId}' не найдена")
                    .ToErrors();
            }

            List<Question> questions = await readDbContext.MaterialsRead
                .OfType<Question>()
                .Where(question =>
                    question.TopicId == topicId &&
                    question.ArticleId == null)
                .ToListAsync(cancellationToken);

            List<Article> articles = await readDbContext.MaterialsRead
                .OfType<Article>()
                .Where(article => article.TopicId == topicId)
                .ToListAsync(cancellationToken);

            var questionOptions = questions
                .OrderBy(
                    question => question.Title.Value,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(Map)
                .ToArray();

            var articleOptions = articles
                .OrderBy(
                    article => article.Title.Value,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(Map)
                .ToArray();

            return new MaterialLearningLinkOptionsDto(
                questionOptions,
                articleOptions);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Загрузка вариантов связей для темы {TopicId} была отменена",
                query.TopicId);

            return CommonErrors.OperationCancelled(
                    "material.learning.options.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось загрузить варианты связей для темы {TopicId}",
                query.TopicId);

            return CommonErrors.Db(
                    "material.learning.options.failed",
                    "Не удалось загрузить вопросы и статьи для настройки связей")
                .ToErrors();
        }
    }

    private static MaterialLearningLinkOptionDto Map(Material material)
    {
        return new MaterialLearningLinkOptionDto(
            material.Id.Value,
            material.Title.Value,
            material.Difficulty.ToString(),
            material.ExperienceRewards.StudyPoints,
            material.ExperienceRewards.ReviewPoints);
    }
}
