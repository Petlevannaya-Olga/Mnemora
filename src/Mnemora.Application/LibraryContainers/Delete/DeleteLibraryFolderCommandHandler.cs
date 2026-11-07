using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.Delete;

public sealed class DeleteLibraryFolderCommandHandler(
    ILibraryContainersRepository containersRepository,
    IReadDbContext readDbContext,
    ITransactionManager transactionManager,
    ILogger<DeleteLibraryFolderCommandHandler> logger)
    : ICommandHandler<Guid, DeleteLibraryFolderCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        DeleteLibraryFolderCommand command,
        CancellationToken cancellationToken)
    {
        var idResult = LibraryContainerId.Create(command.FolderId);
        if (idResult.IsFailure)
            return idResult.Error.ToErrors();

        var folderResult = await containersRepository.GetByIdAsync(
            idResult.Value, cancellationToken);
        if (folderResult.IsFailure)
            return folderResult.Error.ToErrors();

        LibraryContainer? folder = folderResult.Value;
        if (folder is null || folder.IsRoot)
            return CommonErrors.NotFound(
                "library.folder.not.found", "Папка не найдена").ToErrors();

        try
        {
            // Removing the parent is prohibited while nested folders exist.
            bool hasChildren = await readDbContext.LibraryContainersRead
                .AnyAsync(child => child.ParentId == folder.Id, cancellationToken);
            if (hasChildren)
                return CommonErrors.Conflict(
                    "library.folder.delete.has.children",
                    "Сначала удалите вложенные папки").ToErrors();

            // Neither standalone questions nor articles may be deleted implicitly.
            bool hasMaterials = await readDbContext.MaterialsRead
                .AnyAsync(material => material.ContainerId == folder.Id, cancellationToken);
            if (hasMaterials)
                return CommonErrors.Conflict(
                    "library.folder.delete.has.materials",
                    "Сначала удалите или переместите материалы папки").ToErrors();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled("library.folder.delete.check.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось проверить содержимое папки {FolderId}", folder.Id.Value);
            return CommonErrors.Db(
                "library.folder.delete.check.failed", "Не удалось проверить содержимое папки")
                .ToErrors();
        }

        containersRepository.Remove(folder);
        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToErrors();

        if (logger.IsEnabled(LogLevel.Information))
        {
            InfoLog.EmptyLibraryFolderDeleted(logger, folder.Id.Value);
        }
        return folder.Id.Value;
    }
}
