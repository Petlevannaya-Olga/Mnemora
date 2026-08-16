using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Update;

public sealed class UpdateTopicCommandHandler(
    ITopicsRepository topicsRepository,
    ITransactionManager transactionManager,
    ILogger<UpdateTopicCommandHandler> logger)
    : ICommandHandler<Guid, UpdateTopicCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        UpdateTopicCommand command,
        CancellationToken cancellationToken)
    {
        var topicId =  TopicId.Create(command.TopicId).Value;

        var topicResult =
            await topicsRepository.GetByIdAsync(
                topicId,
                cancellationToken);

        if (topicResult.IsFailure)
        {
            return topicResult.Error.ToErrors();
        }

        var topic = topicResult.Value;

        if (topic is null)
        {
            return new Error(
                    "topic.not.found",
                    "Тема не найдена",
                    ErrorType.NOT_FOUND,
                    nameof(command.TopicId))
                .ToErrors();
        }

        var nameResult = TopicName.Create(
            command.Name);

        if (nameResult.IsFailure)
        {
            return nameResult.Error.ToErrors();
        }

        var topicExistsResult =
            await topicsRepository.ExistsAsync(
                candidate =>
                    candidate.Id != topicId &&
                    candidate.SectionId == topic.SectionId &&
                    candidate.Name == nameResult.Value,
                cancellationToken);

        if (topicExistsResult.IsFailure)
        {
            return topicExistsResult.Error.ToErrors();
        }

        if (topicExistsResult.Value)
        {
            return new Error(
                    "topic.name.already.exists",
                    "Тема с таким названием уже существует в этом разделе",
                    ErrorType.CONFLICT,
                    nameof(command.Name))
                .ToErrors();
        }

        topic.Update(
            nameResult.Value,
            command.Color,
            command.Icon);

        var saveResult =
            await transactionManager.SaveChangesAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось обновить тему {TopicId}. " +
                "Код ошибки: {ErrorCode}",
                topic.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Обновлена тема {TopicId} с названием {TopicName} " +
            "в разделе {SectionId}",
            topic.Id.Value,
            topic.Name.Value,
            topic.SectionId.Value);

        return topic.Id.Value;
    }
}