using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Delete;

public sealed class DeleteArticleCommandHandler(
    IMaterialsRepository materialsRepository,
    IMaterialContentStore materialContentStore,
    ITransactionManager transactionManager,
    ILogger<DeleteArticleCommandHandler> logger)
    : ICommandHandler<Guid, DeleteArticleCommand>
{
    public async Task<Result<Guid, Errors>> Handle(DeleteArticleCommand command, CancellationToken cancellationToken)
    {
        var articleIdResult = MaterialId.Create(command.ArticleId);

        if (articleIdResult.IsFailure)
        {
            return articleIdResult.Error.ToErrors();
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

        var questionsResult = await materialsRepository.GetQuestionsByArticleIdAsync(article.Id, cancellationToken);

        if (questionsResult.IsFailure)
        {
            return questionsResult.Error.ToErrors();
        }

        var questions = questionsResult.Value;

        foreach (Question question in questions)
        {
            var detachResult = question.DetachFromArticle();

            if (detachResult.IsFailure)
            {
                return detachResult.Error.ToErrors();
            }
        }

        materialsRepository.Remove(article);
        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось удалить статью {ArticleId}. Код ошибки: {ErrorCode}",
                article.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        var deleteContentResult = materialContentStore.Delete(article.Id, article.Type);

        if (deleteContentResult.IsFailure)
        {
            logger.LogError(
                "Статья {ArticleId} удалена из базы данных, но её Markdown-файлы удалить не удалось. Код ошибки: {ErrorCode}",
                article.Id.Value,
                deleteContentResult.Error.Code);
        }

        logger.LogInformation(
            "Удалена статья {ArticleId} с названием {ArticleTitle}. Откреплено вопросов: {QuestionCount}",
            article.Id.Value,
            article.Title.Value,
            questions.Count);

        return article.Id.Value;
    }
}