using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Topics;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.Update;

public sealed class UpdateQuestionCommandHandler(
    ITopicsRepository topicsRepository,
    IMaterialsRepository materialsRepository,
    ITransactionManager transactionManager,
    ILogger<UpdateQuestionCommandHandler> logger)
    : ICommandHandler<Guid, UpdateQuestionCommand>
{
    public async Task<Result<Guid, Errors>> Handle(UpdateQuestionCommand command, CancellationToken cancellationToken)
    {
        if (command.ArticleId is null && command.TopicId is null)
        {
            return CommonErrors.IsRequired(nameof(command.TopicId)).ToErrors();
        }

        if (command.ArticleId is not null && command.TopicId is not null)
        {
            return CommonErrors.Validation(
                "question.relationship.target.is.ambiguous",
                "Нельзя одновременно указать тему и связанную статью.",
                nameof(command.ArticleId)).ToErrors();
        }

        var questionIdResult = MaterialId.Create(command.QuestionId);

        if (questionIdResult.IsFailure)
        {
            return questionIdResult.Error.ToErrors();
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

        var materialResult = await materialsRepository.GetByIdAsync(questionIdResult.Value, cancellationToken);

        if (materialResult.IsFailure)
        {
            return materialResult.Error.ToErrors();
        }

        if (materialResult.Value is null)
        {
            return new Error(
                "question.not.found",
                "Вопрос не найден",
                ErrorType.NOT_FOUND,
                nameof(command.QuestionId)).ToErrors();
        }

        if (materialResult.Value is not Question question)
        {
            return CommonErrors.Validation(
                "question.id.references.non.question",
                "Указанный материал не является вопросом.",
                nameof(command.QuestionId)).ToErrors();
        }

        UnitResult<Error> relationshipResult;

        if (command.ArticleId is not null)
        {
            var articleIdResult = MaterialId.Create(command.ArticleId.Value);

            if (articleIdResult.IsFailure)
            {
                return articleIdResult.Error.ToErrors();
            }

            var articleResult = await materialsRepository.GetByIdAsync(articleIdResult.Value, cancellationToken);

            if (articleResult.IsFailure)
            {
                return articleResult.Error.ToErrors();
            }

            if (articleResult.Value is null)
            {
                return new Error(
                    "article.not.found",
                    "Статья не найдена",
                    ErrorType.NOT_FOUND,
                    nameof(command.ArticleId)).ToErrors();
            }

            if (articleResult.Value is not Article article)
            {
                return CommonErrors.Validation(
                    "article.id.references.non.article",
                    "Указанный материал не является статьёй.",
                    nameof(command.ArticleId)).ToErrors();
            }

            relationshipResult = AttachToArticle(question, article);
        }
        else
        {
            var topicIdResult = TopicId.Create(command.TopicId!.Value);

            if (topicIdResult.IsFailure)
            {
                return topicIdResult.Error.ToErrors();
            }

            var topicExistsResult = await topicsRepository.ExistsAsync(
                topic => topic.Id == topicIdResult.Value,
                cancellationToken);

            if (topicExistsResult.IsFailure)
            {
                return topicExistsResult.Error.ToErrors();
            }

            if (!topicExistsResult.Value)
            {
                return CommonErrors.NotFound(
                    "topic.not.found",
                    $"Тема с идентификатором '{command.TopicId}' не найдена").ToErrors();
            }

            relationshipResult = MakeStandalone(question, topicIdResult.Value);
        }

        if (relationshipResult.IsFailure)
        {
            return relationshipResult.Error.ToErrors();
        }

        var commonChangesResult = ApplyCommonChanges(
            question,
            titleResult.Value,
            command.Difficulty,
            iconResult.Value,
            rewardsResult.Value,
            tagsResult.Value);

        if (commonChangesResult.IsFailure)
        {
            return commonChangesResult.Error.ToErrors();
        }

        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось изменить вопрос {QuestionId}. Код ошибки: {ErrorCode}",
                question.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Изменён вопрос {QuestionId}. Тема: {TopicId}. Статья: {ArticleId}",
            question.Id.Value,
            question.TopicId.Value,
            question.ArticleId?.Value);

        return question.Id.Value;
    }

    private static UnitResult<Error> AttachToArticle(Question question, Article article)
    {
        if (question.ArticleId == article.Id)
        {
            return question.ChangeTopicWithArticle(article);
        }

        if (question.ArticleId is not null)
        {
            var detachResult = question.DetachFromArticle();

            if (detachResult.IsFailure)
            {
                return detachResult.Error;
            }
        }

        return question.AttachToArticle(article);
    }

    private static UnitResult<Error> MakeStandalone(Question question, TopicId topicId)
    {
        if (question.ArticleId is not null)
        {
            var detachResult = question.DetachFromArticle();

            if (detachResult.IsFailure)
            {
                return detachResult.Error;
            }
        }

        return question.ChangeTopic(topicId);
    }

    private static UnitResult<Error> ApplyCommonChanges(
        Question question,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards rewards,
        IReadOnlyCollection<MaterialTag> tags)
    {
        var titleResult = question.ChangeTitle(title);

        if (titleResult.IsFailure)
        {
            return titleResult.Error;
        }

        var difficultyResult = question.ChangeDifficulty(difficulty);

        if (difficultyResult.IsFailure)
        {
            return difficultyResult.Error;
        }

        var iconResult = question.ChangeIcon(icon);

        if (iconResult.IsFailure)
        {
            return iconResult.Error;
        }

        var rewardsResult = question.ChangeExperienceRewards(rewards);

        if (rewardsResult.IsFailure)
        {
            return rewardsResult.Error;
        }

        return question.ReplaceTags(tags);
    }

    private static Result<MaterialIcon, Error> CreateIcon(string? iconKey)
    {
        return iconKey is null ? MaterialIcon.DefaultQuestion : MaterialIcon.Create(iconKey);
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