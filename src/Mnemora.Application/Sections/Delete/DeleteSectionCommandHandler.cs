using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Delete;

public sealed class DeleteSectionCommandHandler(
    ISectionsRepository sectionsRepository,
    IReadDbContext readDbContext,
    ITransactionManager transactionManager,
    ILogger<DeleteSectionCommandHandler> logger)
    : ICommandHandler<Guid, DeleteSectionCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        DeleteSectionCommand command,
        CancellationToken cancellationToken)
    {
        var sectionIdResult = SectionId.Create(command.SectionId);

        if (sectionIdResult.IsFailure)
        {
            return sectionIdResult.Error.ToErrors();
        }

        SectionId sectionId = sectionIdResult.Value;

        var sectionResult = await sectionsRepository.GetByIdAsync(
            sectionId,
            cancellationToken);

        if (sectionResult.IsFailure)
        {
            return sectionResult.Error.ToErrors();
        }

        Section? section = sectionResult.Value;

        if (section is null)
        {
            return CommonErrors.NotFound(
                    "section.not.found",
                    "Раздел не найден")
                .ToErrors();
        }

        try
        {
            // Проверяем фактические папки раздела, а не устаревшие данные
            // переходного периода.
            bool hasFolders = await readDbContext.LibraryContainersRead
                .AnyAsync(
                    container =>
                        container.SectionId == sectionId &&
                        container.ParentId != null,
                    cancellationToken);

            if (hasFolders)
            {
                return CommonErrors.Conflict(
                        "section.delete.has.folders",
                        "Нельзя удалить раздел, пока в нём есть папки")
                    .ToErrors();
            }

            // Материалы могут находиться непосредственно в корне раздела,
            // даже если пользовательских папок нет.
            var sectionContainerIds = readDbContext.LibraryContainersRead
                .Where(container => container.SectionId == sectionId)
                .Select(container => container.Id);

            bool hasMaterials = await readDbContext.MaterialsRead
                .AnyAsync(
                    material => sectionContainerIds.Contains(material.ContainerId),
                    cancellationToken);

            if (hasMaterials)
            {
                return CommonErrors.Conflict(
                        "section.delete.has.materials",
                        "Нельзя удалить раздел, пока в нём есть материалы")
                    .ToErrors();
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                    "section.delete.check.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось проверить содержимое раздела {SectionId} перед удалением",
                sectionId.Value);

            return CommonErrors.Db(
                    "section.delete.check.failed",
                    "Не удалось проверить содержимое раздела")
                .ToErrors();
        }

        sectionsRepository.Remove(section);

        var saveResult = await transactionManager.SaveChangesAsync(
            cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось удалить раздел {SectionId}. Код ошибки: {ErrorCode}",
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
