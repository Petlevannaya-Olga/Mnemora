using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Topics;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Create;

public sealed class CreateArticleCommandHandler(
    ITopicsRepository topicsRepository,
    IMaterialsRepository materialsRepository,
    IMaterialContentStore materialContentStore,
    ITransactionManager transactionManager,
    ILogger<CreateArticleCommandHandler> logger)
    : ICommandHandler<Guid, CreateArticleCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateArticleCommand command,
        CancellationToken cancellationToken)
    {
        var topicIdResult = TopicId.Create(command.TopicId);

        if (topicIdResult.IsFailure)
        {
            return topicIdResult.Error.ToErrors();
        }

        var titleResult = MaterialTitle.Create(command.Title);

        if (titleResult.IsFailure)
        {
            return titleResult.Error.ToErrors();
        }

        var iconResult = CreateIcon(command.IconKey);

        if (iconResult.IsFailure)
        {
            return iconResult.Error.ToErrors();
        }

        var rewardsResult =
            MaterialExperienceRewards.Create(
                command.StudyPoints,
                command.ReviewPoints);

        if (rewardsResult.IsFailure)
        {
            return rewardsResult.Error.ToErrors();
        }

        var tagsResult = CreateTags(command.Tags);

        if (tagsResult.IsFailure)
        {
            return tagsResult.Error.ToErrors();
        }

        var contentResult = ArticleContent.Create(command.BodyMarkdown);

        if (contentResult.IsFailure)
        {
            return contentResult.Error.ToErrors();
        }

        var topicExistsResult =
            await topicsRepository.ExistsAsync(
                topic =>
                    topic.Id == topicIdResult.Value,
                cancellationToken);

        if (topicExistsResult.IsFailure)
        {
            return topicExistsResult.Error.ToErrors();
        }

        if (!topicExistsResult.Value)
        {
            return CommonErrors.NotFound(
                    "topic.not.found",
                    $"Тема с идентификатором '{command.TopicId}' не найдена")
                .ToErrors();
        }

        var articleResult =
            Article.Create(
                topicIdResult.Value,
                titleResult.Value,
                command.Difficulty,
                iconResult.Value,
                rewardsResult.Value,
                tagsResult.Value);

        if (articleResult.IsFailure)
        {
            return articleResult.Error.ToErrors();
        }

        Article article = articleResult.Value;

        var fileResult =
            await materialContentStore
                .CreateArticleAsync(
                    article.Id,
                    contentResult.Value,
                    cancellationToken);

        if (fileResult.IsFailure)
        {
            return fileResult.Error.ToErrors();
        }

        try
        {
            materialsRepository.Add(article);

            var saveResult =
                await transactionManager.SaveChangesAsync(
                    cancellationToken);

            if (saveResult.IsFailure)
            {
                TryDeleteContent(article);

                logger.LogWarning(
                    "Не удалось сохранить статью {ArticleId}. Код ошибки: {ErrorCode}",
                    article.Id.Value,
                    saveResult.Error.Code);

                return saveResult.Error.ToErrors();
            }
        }
        catch
        {
            TryDeleteContent(article);
            throw;
        }

        logger.LogInformation(
            "Создана статья {ArticleId} с названием {ArticleTitle} в теме {TopicId}",
            article.Id.Value,
            article.Title.Value,
            article.TopicId.Value);

        return article.Id.Value;
    }

    private static Result<MaterialIcon?, Error> CreateIcon(string? iconKey)
    {
        if (iconKey is null)
        {
            return Result.Success<MaterialIcon?, Error>(null);
        }

        var iconResult = MaterialIcon.Create(iconKey);

        if (iconResult.IsFailure)
        {
            return iconResult.Error;
        }

        return Result.Success<MaterialIcon?, Error>(iconResult.Value);
    }

    private static Result<IReadOnlyCollection<MaterialTag>, Error> CreateTags(IReadOnlyCollection<string>? tags)
    {
        if (tags is null)
        {
            return Result.Success<
                IReadOnlyCollection<MaterialTag>,
                Error>(
                Array.Empty<MaterialTag>());
        }

        var materialTags = new List<MaterialTag>(tags.Count);

        foreach (string tag in tags)
        {
            var tagResult = MaterialTag.Create(tag);

            if (tagResult.IsFailure)
            {
                return tagResult.Error;
            }

            materialTags.Add(tagResult.Value);
        }

        return Result.Success<
            IReadOnlyCollection<MaterialTag>,
            Error>(materialTags);
    }

    private void TryDeleteContent(Article article)
    {
        var deleteResult =
            materialContentStore.Delete(
                article.Id,
                article.Type);

        if (deleteResult.IsFailure)
        {
            logger.LogError(
                "Не удалось удалить Markdown-файлы статьи {ArticleId} после ошибки сохранения. Код ошибки: {ErrorCode}",
                article.Id.Value,
                deleteResult.Error.Code);
        }
    }
}