using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Update;

public sealed class UpdateSectionCommandHandler(
    ISectionsRepository sectionsRepository,
    ITransactionManager transactionManager,
    ILogger<UpdateSectionCommandHandler> logger)
    : ICommandHandler<Guid, UpdateSectionCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        UpdateSectionCommand command,
        CancellationToken cancellationToken)
    {
        var sectionId = new SectionId(
            command.SectionId);

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

        var nameResult = SectionName.Create(
            command.Name);

        if (nameResult.IsFailure)
        {
            return nameResult.Error.ToErrors();
        }

        var sectionExistsResult =
            await sectionsRepository.ExistsAsync(
                candidate =>
                    candidate.Id != sectionId &&
                    candidate.Name == nameResult.Value,
                cancellationToken);

        if (sectionExistsResult.IsFailure)
        {
            return sectionExistsResult.Error.ToErrors();
        }

        if (sectionExistsResult.Value)
        {
            return new Error(
                    "section.name.already.exists",
                    "Раздел с таким названием уже существует",
                    ErrorType.CONFLICT,
                    nameof(command.Name))
                .ToErrors();
        }

        section.Update(
            nameResult.Value,
            command.Color,
            command.Icon);

        var saveResult =
            await transactionManager.SaveChangesAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось обновить раздел {SectionId}. " +
                "Код ошибки: {ErrorCode}",
                section.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Обновлён раздел {SectionId} с названием {SectionName}",
            section.Id.Value,
            section.Name.Value);

        return section.Id.Value;
    }
}