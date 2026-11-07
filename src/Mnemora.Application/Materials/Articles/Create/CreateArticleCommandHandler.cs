using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Create;

public sealed class CreateArticleCommandHandler(
    ILibraryContainersRepository libraryContainersRepository,
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
        var containerIdResult = LibraryContainerId.Create(command.ContainerId);

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
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

        var containerExistsResult =
            await libraryContainersRepository.ExistsAsync(
                container =>
                    container.Id == containerIdResult.Value,
                cancellationToken);

        if (containerExistsResult.IsFailure)
        {
            return containerExistsResult.Error.ToErrors();
        }

        if (!containerExistsResult.Value)
        {
            return CommonErrors.NotFound(
                    "container.not.found",
                    $"Контейнер с идентификатором '{command.ContainerId}' не найден")
                .ToErrors();
        }

        var articleResult =
            Article.CreateInContainer(
                containerIdResult.Value,
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

        if (logger.IsEnabled(LogLevel.Information))
        {
            InfoLog.ArticleCreated(logger, article.Id.Value, article.Title.Value, article.ContainerId.Value);
        }

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