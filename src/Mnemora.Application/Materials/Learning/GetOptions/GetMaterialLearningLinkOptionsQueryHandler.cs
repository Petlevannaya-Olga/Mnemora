using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.LibraryContainers;
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
        var containerIdResult = LibraryContainerId.Create(query.ContainerId);

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
        }

        LibraryContainerId containerId = containerIdResult.Value;

        try
        {
            bool containerExists = await readDbContext.LibraryContainersRead
                .AnyAsync(
                    container => container.Id == containerId,
                    cancellationToken);

            if (!containerExists)
            {
                return CommonErrors.NotFound(
                        "library.container.not.found",
                        $"Контейнер библиотеки с идентификатором '{query.ContainerId}' не найден")
                    .ToErrors();
            }

            List<Question> questions = await readDbContext.MaterialsRead
                .OfType<Question>()
                .Where(question =>
                    question.ContainerId == containerId &&
                    question.ArticleId == null)
                .ToListAsync(cancellationToken);

            List<Article> articles = await readDbContext.MaterialsRead
                .OfType<Article>()
                .Where(article => article.ContainerId == containerId)
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
                "Загрузка вариантов связей для контейнера {ContainerId} была отменена",
                query.ContainerId);

            return CommonErrors.OperationCancelled(
                    "material.learning.options.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось загрузить варианты связей для контейнера {ContainerId}",
                query.ContainerId);

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
