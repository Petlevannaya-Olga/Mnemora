using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Update;

public sealed class UpdateArticleCommandHandler(
    ILibraryContainersRepository libraryContainersRepository,
    IMaterialsRepository materialsRepository,
    ITransactionManager transactionManager,
    ILogger<UpdateArticleCommandHandler> logger)
    : ICommandHandler<Guid, UpdateArticleCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        UpdateArticleCommand command,
        CancellationToken cancellationToken)
    {
        var articleIdResult = MaterialId.Create(command.ArticleId);

        if (articleIdResult.IsFailure)
        {
            return articleIdResult.Error.ToErrors();
        }

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

        var rewardsResult = MaterialExperienceRewards.Create(command.StudyPoints, command.ReviewPoints);

        if (rewardsResult.IsFailure)
        {
            return rewardsResult.Error.ToErrors();
        }

        var tagsResult = CreateTags(command.Tags);

        if (tagsResult.IsFailure)
        {
            return tagsResult.Error.ToErrors();
        }

        var materialResult = await materialsRepository.GetByIdAsync(articleIdResult.Value, cancellationToken);

        if (materialResult.IsFailure)
        {
            return materialResult.Error.ToErrors();
        }

        if (materialResult.Value is null)
        {
            return new Error(
                "article.not.found",
                "Статья не найдена",
                ErrorType.NOT_FOUND,
                nameof(command.ArticleId)).ToErrors();
        }

        if (materialResult.Value is not Article article)
        {
            return CommonErrors.Validation(
                "article.id.references.non.article",
                "Указанный материал не является статьёй.",
                nameof(command.ArticleId)).ToErrors();
        }

        LibraryContainerId newContainerId = containerIdResult.Value;
        var containerChanged = article.ContainerId != newContainerId;
        IReadOnlyCollection<Question> linkedQuestions = Array.Empty<Question>();

        if (containerChanged)
        {
            var containerExistsResult = await libraryContainersRepository.ExistsAsync(
                container => container.Id == newContainerId,
                cancellationToken);

            if (containerExistsResult.IsFailure)
            {
                return containerExistsResult.Error.ToErrors();
            }

            if (!containerExistsResult.Value)
            {
                return CommonErrors.NotFound(
                    "container.not.found",
                    $"Контейнер с идентификатором '{command.ContainerId}' не найден").ToErrors();
            }

            var questionsResult = await materialsRepository.GetQuestionsByArticleIdAsync(article.Id, cancellationToken);

            if (questionsResult.IsFailure)
            {
                return questionsResult.Error.ToErrors();
            }

            linkedQuestions = questionsResult.Value;
        }

        var updateResult = ApplyChanges(
            article,
            linkedQuestions,
            newContainerId,
            titleResult.Value,
            command.Difficulty,
            iconResult.Value,
            rewardsResult.Value,
            tagsResult.Value);

        if (updateResult.IsFailure)
        {
            return updateResult.Error.ToErrors();
        }

        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось изменить статью {ArticleId}. Код ошибки: {ErrorCode}",
                article.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            InfoLog.ArticleUpdated(logger, article.Id.Value, article.ContainerId.Value, linkedQuestions.Count);
        }

        return article.Id.Value;
    }

    private static UnitResult<Error> ApplyChanges(
        Article article,
        IReadOnlyCollection<Question> linkedQuestions,
        LibraryContainerId containerId,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards rewards,
        IReadOnlyCollection<MaterialTag> tags)
    {
        var changeContainerResult = article.MoveToContainer(containerId);

        if (changeContainerResult.IsFailure)
        {
            return changeContainerResult.Error;
        }

        foreach (Question question in linkedQuestions)
        {
            var questionResult = question.MoveToContainerWithArticle(article);

            if (questionResult.IsFailure)
            {
                return questionResult.Error;
            }
        }

        var changeTitleResult = article.ChangeTitle(title);

        if (changeTitleResult.IsFailure)
        {
            return changeTitleResult.Error;
        }

        var changeDifficultyResult = article.ChangeDifficulty(difficulty);

        if (changeDifficultyResult.IsFailure)
        {
            return changeDifficultyResult.Error;
        }

        var changeIconResult = article.ChangeIcon(icon);

        if (changeIconResult.IsFailure)
        {
            return changeIconResult.Error;
        }

        var changeRewardsResult = article.ChangeExperienceRewards(rewards);

        if (changeRewardsResult.IsFailure)
        {
            return changeRewardsResult.Error;
        }

        var replaceTagsResult = article.ReplaceTags(tags);
        return replaceTagsResult.IsFailure ? replaceTagsResult.Error : UnitResult.Success<Error>();
    }

    private static Result<MaterialIcon, Error> CreateIcon(string? iconKey)
    {
        return iconKey is null ? MaterialIcon.DefaultArticle : MaterialIcon.Create(iconKey);
    }

    private static Result<IReadOnlyCollection<MaterialTag>, Error> CreateTags(IReadOnlyCollection<string>? tags)
    {
        if (tags is null)
        {
            return Result.Success<IReadOnlyCollection<MaterialTag>, Error>(Array.Empty<MaterialTag>());
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

        return Result.Success<IReadOnlyCollection<MaterialTag>, Error>(materialTags);
    }
}