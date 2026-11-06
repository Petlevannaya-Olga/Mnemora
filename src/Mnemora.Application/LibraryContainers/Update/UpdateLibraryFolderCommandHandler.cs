using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.Update;

public sealed class UpdateLibraryFolderCommandHandler(
    ILibraryContainersRepository containersRepository,
    ITransactionManager transactionManager,
    ILogger<UpdateLibraryFolderCommandHandler> logger)
    : ICommandHandler<Guid, UpdateLibraryFolderCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        UpdateLibraryFolderCommand command,
        CancellationToken cancellationToken)
    {
        var idResult = LibraryContainerId.Create(command.FolderId);
        if (idResult.IsFailure)
            return idResult.Error.ToErrors();

        var nameResult = FolderName.Create(command.Name);
        if (nameResult.IsFailure)
            return nameResult.Error.ToErrors();

        var folderResult = await containersRepository.GetByIdAsync(
            idResult.Value, cancellationToken);
        if (folderResult.IsFailure)
            return folderResult.Error.ToErrors();

        LibraryContainer? folder = folderResult.Value;
        if (folder is null || folder.IsRoot)
            return CommonErrors.NotFound(
                "library.folder.not.found", "Папка не найдена").ToErrors();

        var duplicateResult = await containersRepository.ExistsAsync(
            candidate => candidate.Id != folder.Id &&
                         candidate.ParentId == folder.ParentId &&
                         candidate.Name == nameResult.Value,
            cancellationToken);
        if (duplicateResult.IsFailure)
            return duplicateResult.Error.ToErrors();
        if (duplicateResult.Value)
            return CommonErrors.Conflict(
                "library.container.folder.name.already.exists",
                "В этой папке уже есть папка с таким названием").ToErrors();

        var updateResult = folder.UpdateFolder(
            nameResult.Value, command.Color, command.Icon);
        if (updateResult.IsFailure)
            return updateResult.Error.ToErrors();

        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToErrors();

        logger.LogInformation("Обновлена папка {FolderId}", folder.Id.Value);
        return folder.Id.Value;
    }
}
