using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Topics;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Delete;

public sealed class DeleteSectionCommandHandler(
    ISectionsRepository sectionsRepository,
    ITopicsRepository topicsRepository,
    ITransactionManager transactionManager,
    ILogger<DeleteSectionCommandHandler> logger)
    : ICommandHandler<Guid, DeleteSectionCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        DeleteSectionCommand command,
        CancellationToken cancellationToken)
    {
        var sectionId =  SectionId.Create(command.SectionId).Value;

        var sectionResult =
            await sectionsRepository.GetByIdAsync(
                sectionId,
                cancellationToken);

        if (sectionResult.IsFailure)
        {
            return sectionResult.Error.ToErrors();
        }

        var section = sectionResult.Value;

        if (section is null)
        {
            return new Error(
                    "section.not.found",
                    "Раздел не найден",
                    ErrorType.NOT_FOUND,
                    nameof(command.SectionId))
                .ToErrors();
        }

        var hasTopicsResult =
            await topicsRepository.ExistsAsync(
                topic =>
                    topic.SectionId == sectionId,
                cancellationToken);

        if (hasTopicsResult.IsFailure)
        {
            return hasTopicsResult.Error.ToErrors();
        }

        if (hasTopicsResult.Value)
        {
            return new Error(
                    "section.delete.has.topics",
                    "Нельзя удалить раздел, пока в нём есть темы",
                    ErrorType.CONFLICT,
                    nameof(command.SectionId))
                .ToErrors();
        }

        sectionsRepository.Remove(
            section);

        var saveResult =
            await transactionManager.SaveChangesAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось удалить раздел {SectionId}. " +
                "Код ошибки: {ErrorCode}",
                section.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Удалён раздел {SectionId} с названием {SectionName}",
            section.Id.Value,
            section.Name.Value);

        return section.Id.Value;
    }
}