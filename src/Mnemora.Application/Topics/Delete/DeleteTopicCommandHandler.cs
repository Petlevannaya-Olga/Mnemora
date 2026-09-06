using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Delete;

public sealed class DeleteTopicCommandHandler(
    ITopicsRepository topicsRepository,
    ILibraryContainersRepository libraryContainersRepository,
    ITransactionManager transactionManager,
    ILogger<DeleteTopicCommandHandler> logger)
    : ICommandHandler<Guid, DeleteTopicCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        DeleteTopicCommand command,
        CancellationToken cancellationToken)
    {
        var topicId = TopicId.Create(command.TopicId).Value;

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

        var containerId =
            LibraryContainerId.Create(topic.Id.Value).Value;

        var folderResult =
            await libraryContainersRepository.GetByIdAsync(
                containerId,
                cancellationToken);

        if (folderResult.IsFailure)
        {
            return folderResult.Error.ToErrors();
        }

        var folder = folderResult.Value;

        if (folder is null || !folder.IsFolder)
        {
            return CommonErrors.Failure(
                    "legacy.topic.folder.missing",
                    "Для темы не найдена соответствующая папка библиотеки")
                .ToErrors();
        }

        topicsRepository.Remove(topic);
        libraryContainersRepository.Remove(folder);

        var saveResult =
            await transactionManager.SaveChangesAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось удалить тему {TopicId}. " +
                "Код ошибки: {ErrorCode}",
                topic.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Удалена тема {TopicId} с названием {TopicName} " +
            "из раздела {SectionId}",
            topic.Id.Value,
            topic.Name.Value,
            topic.SectionId.Value);

        return topic.Id.Value;
    }
}
