using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Application.Sections;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Create;

public sealed class CreateTopicCommandHandler(
    ISectionsRepository sectionsRepository,
    ITopicsRepository topicsRepository,
    ILibraryContainersRepository libraryContainersRepository,
    ITransactionManager transactionManager,
    ILogger<CreateTopicCommandHandler> logger)
    : ICommandHandler<Guid, CreateTopicCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateTopicCommand command,
        CancellationToken cancellationToken)
    {
        var nameResult = TopicName.Create(
            command.Name);

        if (nameResult.IsFailure)
        {
            return nameResult.Error.ToErrors();
        }

        var sectionId = SectionId.Create(command.SectionId).Value;

        var sectionExistsResult =
            await sectionsRepository.ExistsAsync(
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

        var topicExistsResult =
            await topicsRepository.ExistsAsync(
                topic =>
                    topic.SectionId == sectionId &&
                    topic.Name == nameResult.Value,
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

        var rootResult =
            await libraryContainersRepository.GetRootBySectionIdAsync(
                sectionId,
                cancellationToken);

        if (rootResult.IsFailure)
        {
            return rootResult.Error.ToErrors();
        }

        if (rootResult.Value is null)
        {
            return CommonErrors.Failure(
                    "library.container.root.missing",
                    "Для раздела не найден корневой контейнер библиотеки")
                .ToErrors();
        }

        var topic = Topic.Create(
            sectionId,
            nameResult.Value,
            command.Color,
            command.Icon);

        var containerId =
            LibraryContainerId.Create(topic.Id.Value).Value;

        var folderResult = LibraryContainer.CreateFolderWithId(
            containerId,
            rootResult.Value,
            LegacyTopicFolderMapper.ToFolderName(topic.Name),
            LegacyTopicFolderMapper.ToFolderColor(topic.Color),
            LegacyTopicFolderMapper.ToFolderIcon(topic.Icon));

        if (folderResult.IsFailure)
        {
            return folderResult.Error.ToErrors();
        }

        topicsRepository.Add(topic);
        libraryContainersRepository.Add(folderResult.Value);

        var saveResult =
            await transactionManager.SaveChangesAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось создать тему в разделе {SectionId}. " +
                "Код ошибки: {ErrorCode}",
                command.SectionId,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Создана тема {TopicId} с названием {TopicName} " +
            "в разделе {SectionId}. Цвет: {TopicColor}, иконка: {TopicIcon}",
            topic.Id.Value,
            topic.Name.Value,
            command.SectionId,
            topic.Color,
            topic.Icon);

        return topic.Id.Value;
    }
}
