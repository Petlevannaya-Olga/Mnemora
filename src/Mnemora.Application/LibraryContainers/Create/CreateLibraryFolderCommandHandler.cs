using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.Create;

public sealed class CreateLibraryFolderCommandHandler(
    ILibraryContainersRepository libraryContainersRepository,
    ITransactionManager transactionManager,
    ILogger<CreateLibraryFolderCommandHandler> logger)
    : ICommandHandler<Guid, CreateLibraryFolderCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateLibraryFolderCommand command,
        CancellationToken cancellationToken)
    {
        var parentIdResult = LibraryContainerId.Create(command.ParentContainerId);
        if (parentIdResult.IsFailure)
            return parentIdResult.Error.ToErrors();

        var nameResult = FolderName.Create(command.Name);
        if (nameResult.IsFailure)
            return nameResult.Error.ToErrors();

        var parentResult = await libraryContainersRepository.GetByIdAsync(
            parentIdResult.Value,
            cancellationToken);

        if (parentResult.IsFailure)
            return parentResult.Error.ToErrors();

        if (parentResult.Value is null)
        {
            return CommonErrors.NotFound(
                    "library.container.parent.not.found",
                    $"Родительский контейнер '{command.ParentContainerId}' не найден")
                .ToErrors();
        }

        LibraryContainer parent = parentResult.Value;

        var duplicateResult = await libraryContainersRepository.ExistsAsync(
            container =>
                container.ParentId == parent.Id &&
                container.Name == nameResult.Value,
            cancellationToken);

        if (duplicateResult.IsFailure)
            return duplicateResult.Error.ToErrors();

        if (duplicateResult.Value)
        {
            return new Error(
                    "library.container.folder.name.already.exists",
                    "В этой папке уже есть папка с таким названием",
                    ErrorType.CONFLICT,
                    nameof(CreateLibraryFolderCommand.Name))
                .ToErrors();
        }

        var folderResult = LibraryContainer.CreateFolder(
            parent,
            nameResult.Value,
            command.Color,
            command.Icon);

        if (folderResult.IsFailure)
            return folderResult.Error.ToErrors();

        LibraryContainer folder = folderResult.Value;
        libraryContainersRepository.Add(folder);

        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToErrors();

        logger.LogInformation(
            "Создана папка библиотеки {FolderId} в контейнере {ParentContainerId}",
            folder.Id.Value,
            parent.Id.Value);

        return folder.Id.Value;
    }
}
