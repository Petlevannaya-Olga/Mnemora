using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Sections;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Create;

public sealed class CreateTopicCommandHandler(
    ISectionsRepository sectionsRepository,
    ITopicsRepository topicsRepository,
    ITransactionManager transactionManager,
    ILogger<CreateTopicCommandHandler> logger)
    : ICommandHandler<Guid, CreateTopicCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateTopicCommand command,
        CancellationToken cancellationToken)
    {
        var nameResult = TopicName.Create(command.Name);

        if (nameResult.IsFailure)
        {
            return nameResult.Error.ToErrors();
        }

        var sectionId = new SectionId(command.SectionId);

        var sectionExistsResult = await sectionsRepository.ExistsAsync(
            section => section.Id == sectionId,
            cancellationToken);

        if (sectionExistsResult.IsFailure)
        {
            return sectionExistsResult.Error.ToErrors();
        }

        if (!sectionExistsResult.Value)
        {
            return CommonErrors.NotFound(
                    "section.not.found",
                    $"Раздел с идентификатором '{command.SectionId}' не найден")
                .ToErrors();
        }

        var topicExistsResult = await topicsRepository.ExistsAsync(
            topic => topic.SectionId == sectionId
                     && topic.Name == nameResult.Value,
            cancellationToken);

        if (topicExistsResult.IsFailure)
        {
            return topicExistsResult.Error.ToErrors();
        }

        if (topicExistsResult.Value)
        {
            return new Error(
                    "topic.name.already.exists",
                    "Тема с таким названием уже существует в выбранном разделе",
                    ErrorType.CONFLICT,
                    nameof(CreateTopicCommand.Name))
                .ToErrors();
        }

        var topic = Topic.Create(sectionId, nameResult.Value);

        topicsRepository.Add(topic);

        var saveResult = await transactionManager.SaveChangesAsync(
            cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось создать тему в разделе {SectionId}. Код ошибки: {ErrorCode}",
                command.SectionId,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Создана тема {TopicId} с названием {TopicName} в разделе {SectionId}",
            topic.Id.Value,
            topic.Name.Value,
            command.SectionId);

        return topic.Id.Value;
    }
}