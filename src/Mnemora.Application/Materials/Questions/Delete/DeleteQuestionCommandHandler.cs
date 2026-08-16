using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.Delete;

public sealed class DeleteQuestionCommandHandler(
    IMaterialsRepository materialsRepository,
    IMaterialContentStore materialContentStore,
    ITransactionManager transactionManager,
    ILogger<DeleteQuestionCommandHandler> logger)
    : ICommandHandler<Guid, DeleteQuestionCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        DeleteQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var questionIdResult = MaterialId.Create(command.QuestionId);

        if (questionIdResult.IsFailure)
        {
            return questionIdResult.Error.ToErrors();
        }

        var materialResult = await materialsRepository
            .GetByIdAsync(questionIdResult.Value, cancellationToken);

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
                nameof(command.QuestionId))
                .ToErrors();
        }

        if (materialResult.Value is not Question question)
        {
            return CommonErrors.Validation(
                "question.id.references.non.question",
                "Указанный материал не является вопросом.",
                nameof(command.QuestionId))
                .ToErrors();
        }

        materialsRepository.Remove(question);
        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось удалить вопрос {QuestionId}. Код ошибки: {ErrorCode}",
                question.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        var deleteContentResult = materialContentStore.Delete(question.Id, question.Type);

        if (deleteContentResult.IsFailure)
        {
            logger.LogError(
                "Вопрос {QuestionId} удалён из базы данных, но его Markdown-файлы удалить не удалось. Код ошибки: {ErrorCode}",
                question.Id.Value,
                deleteContentResult.Error.Code);
        }

        logger.LogInformation(
            "Удалён вопрос {QuestionId} с названием {QuestionTitle}",
            question.Id.Value,
            question.Title.Value);

        return question.Id.Value;
    }
}