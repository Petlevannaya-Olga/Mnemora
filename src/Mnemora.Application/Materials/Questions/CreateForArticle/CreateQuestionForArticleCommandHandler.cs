using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.CreateForArticle;

public sealed class CreateQuestionForArticleCommandHandler(
    IMaterialsRepository materialsRepository,
    IMaterialContentStore materialContentStore,
    ITransactionManager transactionManager,
    ILogger<CreateQuestionForArticleCommandHandler> logger)
    : ICommandHandler<Guid, CreateQuestionForArticleCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateQuestionForArticleCommand command,
        CancellationToken cancellationToken)
    {
        var articleIdResult = MaterialId.Create(command.ArticleId);

        if (articleIdResult.IsFailure)
        {
            return articleIdResult.Error.ToErrors();
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

        var contentResult = QuestionContent.Create(command.PromptMarkdown, command.ReferenceAnswerMarkdown);

        if (contentResult.IsFailure)
        {
            return contentResult.Error.ToErrors();
        }

        var materialResult = await materialsRepository.GetByIdAsync(articleIdResult.Value, cancellationToken);

        if (materialResult.IsFailure)
        {
            return materialResult.Error.ToErrors();
        }

        if (materialResult.Value is null)
        {
            return CommonErrors.NotFound(
                "article.not.found",
                $"Статья с идентификатором '{command.ArticleId}' не найдена").ToErrors();
        }

        if (materialResult.Value is not Article article)
        {
            return CommonErrors.Validation(
                "article.id.references.non.article",
                "Указанный материал не является статьёй.",
                nameof(command.ArticleId)).ToErrors();
        }

        var questionResult = Question.CreateForArticle(
            article,
            titleResult.Value,
            command.Difficulty,
            iconResult.Value,
            rewardsResult.Value,
            tagsResult.Value);

        if (questionResult.IsFailure)
        {
            return questionResult.Error.ToErrors();
        }

        Question question = questionResult.Value;

        var fileResult = await materialContentStore.CreateQuestionAsync(question.Id, contentResult.Value, cancellationToken);

        if (fileResult.IsFailure)
        {
            return fileResult.Error.ToErrors();
        }

        try
        {
            materialsRepository.Add(question);
            var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);

            if (saveResult.IsFailure)
            {
                TryDeleteContent(question);

                logger.LogWarning(
                    "Не удалось сохранить вопрос {QuestionId}. Код ошибки: {ErrorCode}",
                    question.Id.Value,
                    saveResult.Error.Code);

                return saveResult.Error.ToErrors();
            }
        }
        catch
        {
            TryDeleteContent(question);
            throw;
        }

        logger.LogInformation(
            "Создан вопрос {QuestionId}, связанный со статьёй {ArticleId}",
            question.Id.Value,
            article.Id.Value);

        return question.Id.Value;
    }

    private static Result<MaterialIcon?, Error> CreateIcon(string? iconKey)
    {
        if (iconKey is null)
        {
            return Result.Success<MaterialIcon?, Error>(null);
        }

        var iconResult = MaterialIcon.Create(iconKey);
        return iconResult.IsFailure
            ? iconResult.Error
            : Result.Success<MaterialIcon?, Error>(iconResult.Value);
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

    private void TryDeleteContent(Question question)
    {
        var deleteResult = materialContentStore.Delete(question.Id, question.Type);

        if (deleteResult.IsFailure)
        {
            logger.LogError(
                "Не удалось удалить Markdown-файлы вопроса {QuestionId} после ошибки сохранения. Код ошибки: {ErrorCode}",
                question.Id.Value,
                deleteResult.Error.Code);
        }
    }
}