using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetContainerContents;

public sealed class GetLibraryContainerContentsQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryContainerContentsQueryHandler> logger)
    : IQueryHandler<
        LibraryContainerContentsDto,
        GetLibraryContainerContentsQuery>
{
    public async Task<Result<LibraryContainerContentsDto, Errors>> Handle(
        GetLibraryContainerContentsQuery request,
        CancellationToken cancellationToken = default)
    {
        var containerIdResult =
            LibraryContainerId.Create(request.ContainerId);

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
        }

        LibraryContainerId containerId =
            containerIdResult.Value;

        try
        {
            var containerRow = await (
                    from containerEntity in readDbContext.LibraryContainersRead
                    join sectionEntity in readDbContext.SectionsRead
                        on containerEntity.SectionId equals sectionEntity.Id
                    where containerEntity.Id == containerId
                    select new
                    {
                        Container = containerEntity,
                        Section = sectionEntity,
                    })
                .SingleOrDefaultAsync(cancellationToken);

            if (containerRow is null)
            {
                return CommonErrors.NotFound(
                        "library.container.not.found",
                        $"Контейнер библиотеки с идентификатором '{request.ContainerId}' не найден")
                    .ToErrors();
            }

            LibraryContainer currentContainer =
                containerRow.Container;

            var section =
                containerRow.Section;

            int foldersCount =
                await readDbContext.LibraryContainersRead
                    .CountAsync(
                        folder =>
                            folder.ParentId == containerId,
                        cancellationToken);

            int materialsCount =
                await GetTopLevelMaterials(
                        readDbContext.MaterialsRead)
                    .CountAsync(
                        material =>
                            material.ContainerId == containerId,
                        cancellationToken);

            string containerName =
                currentContainer.IsRoot
                    ? section.Name.Value
                    : currentContainer.Name!.Value;

            string containerColor =
                currentContainer.IsRoot
                    ? section.Color.ToString()
                    : currentContainer.Color!.Value.ToString();

            string containerIcon =
                currentContainer.IsRoot
                    ? section.Icon.ToString()
                    : currentContainer.Icon!.Value.ToString();

            var containerDto =
                new LibraryContainerHeaderDto(
                    currentContainer.Id.Value,
                    section.Id.Value,
                    section.Name.Value,
                    currentContainer.ParentId?.Value,
                    currentContainer.Depth,
                    containerName,
                    containerColor,
                    containerIcon,
                    currentContainer.CreatedAt,
                    currentContainer.UpdatedAt);

            var sectionDto =
                new LibrarySectionHeaderDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.Color.ToString(),
                    section.Icon.ToString(),
                    section.CreatedAt,
                    section.UpdatedAt);

            var result =
                new LibraryContainerContentsDto(
                    containerDto,
                    sectionDto,
                    foldersCount,
                    materialsCount,
                    CanCreateChildFolder(currentContainer));

            return Result.Success<
                LibraryContainerContentsDto,
                Errors>(result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение содержимого контейнера {ContainerId} было отменено",
                request.ContainerId);

            return CommonErrors.OperationCancelled(
                    "library.container.contents.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить содержимое контейнера {ContainerId}",
                request.ContainerId);

            return CommonErrors.Db(
                    "library.container.contents.failed",
                    "Не удалось загрузить содержимое папки")
                .ToErrors();
        }
    }

    private static IQueryable<Material> GetTopLevelMaterials(
        IQueryable<Material> materials) =>
        materials.Where(material =>
            material is Article ||
            (material is Question &&
             ((Question)material).ArticleId == null));

    private static bool CanCreateChildFolder(
        LibraryContainer container) =>
        container.Depth <
        LibraryContainer.MaxFolderDepth;
}
