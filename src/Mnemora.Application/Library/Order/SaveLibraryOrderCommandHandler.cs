using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Order;

public sealed class SaveLibraryOrderCommandHandler(
    ILibraryOrderRepository libraryOrderRepository,
    ITransactionManager transactionManager,
    ILogger<SaveLibraryOrderCommandHandler> logger)
    : ICommandHandler<SaveLibraryOrderCommand>
{
    public async Task<UnitResult<Errors>> Handle(
        SaveLibraryOrderCommand command,
        CancellationToken cancellationToken)
    {
        var changeResult = command.Target switch
        {
            LibraryOrderTarget.SECTIONS => await ChangeSectionsAsync(command.OrderedIds, cancellationToken),
            LibraryOrderTarget.FOLDERS => await ChangeFoldersAsync(command.ParentId, command.OrderedIds, cancellationToken),
            LibraryOrderTarget.MATERIALS => await ChangeMaterialsAsync(command.ParentId, command.OrderedIds, cancellationToken),
            _ => UnitResult.Failure(CommonErrors.Validation(
                "library.order.target.invalid",
                "Выбран неизвестный уровень библиотеки",
                nameof(command.Target)))
        };

        if (changeResult.IsFailure)
        {
            return changeResult.Error.ToErrors();
        }

        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            return saveResult.Error.ToErrors();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            InfoLog.LibraryOrderSaved(logger, command.Target, command.OrderedIds.Count);
        }
        return UnitResult.Success<Errors>();
    }

    private async Task<UnitResult<Error>> ChangeSectionsAsync(
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken)
    {
        var sectionsResult = await libraryOrderRepository.GetSectionsAsync(cancellationToken);

        if (sectionsResult.IsFailure)
        {
            return sectionsResult.Error;
        }

        return ApplyOrder(
            sectionsResult.Value,
            orderedIds,
            section => section.Id.Value,
            (section, position) => section.ChangeDisplayOrder(position));
    }

    private async Task<UnitResult<Error>> ChangeFoldersAsync(
        Guid? parentId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken)
    {
        var sectionIdResult = parentId.HasValue
            ? SectionId.Create(parentId.Value)
            : Result.Failure<SectionId, Error>(CommonErrors.IsRequired("sectionId"));

        if (sectionIdResult.IsFailure)
        {
            return sectionIdResult.Error;
        }

        var foldersResult = await libraryOrderRepository.GetFirstLevelFoldersAsync(
            sectionIdResult.Value,
            cancellationToken);

        if (foldersResult.IsFailure)
        {
            return foldersResult.Error;
        }

        return ApplyOrder(
            foldersResult.Value,
            orderedIds,
            folder => folder.Id.Value,
            (folder, position) => folder.ChangeDisplayOrder(position));
    }

    private async Task<UnitResult<Error>> ChangeMaterialsAsync(
        Guid? parentId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken)
    {
        var containerIdResult = parentId.HasValue
            ? LibraryContainerId.Create(parentId.Value)
            : Result.Failure<LibraryContainerId, Error>(CommonErrors.IsRequired("containerId"));

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error;
        }

        var materialsResult = await libraryOrderRepository.GetMaterialsAsync(containerIdResult.Value, cancellationToken);

        if (materialsResult.IsFailure)
        {
            return materialsResult.Error;
        }

        return ApplyOrder(
            materialsResult.Value,
            orderedIds,
            material => material.Id.Value,
            (material, position) => material.ChangeDisplayOrder(position));
    }

    private static UnitResult<Error> ApplyOrder<TEntity>(
        IReadOnlyList<TEntity> entities,
        IReadOnlyList<Guid> orderedIds,
        Func<TEntity, Guid> getId,
        Action<TEntity, int> setOrder)
    {
        var entitiesById = entities.ToDictionary(getId);

        if (entities.Count != orderedIds.Count || orderedIds.Any(id => !entitiesById.ContainsKey(id)))
        {
            return CommonErrors.Conflict(
                "library.order.items.changed",
                "Состав элементов изменился. Обновите список и повторите перестановку");
        }

        for (int index = 0; index < orderedIds.Count; index++)
        {
            setOrder(entitiesById[orderedIds[index]], index);
        }

        return UnitResult.Success<Error>();
    }
}
