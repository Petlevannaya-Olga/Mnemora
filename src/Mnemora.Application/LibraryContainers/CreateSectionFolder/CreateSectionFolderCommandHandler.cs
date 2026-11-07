using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.CreateSectionFolder;

public sealed class CreateSectionFolderCommandHandler(
    ILibraryContainersRepository containersRepository,
    ITransactionManager transactionManager,
    ILogger<CreateSectionFolderCommandHandler> logger)
    : ICommandHandler<Guid, CreateSectionFolderCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateSectionFolderCommand command,
        CancellationToken cancellationToken)
    {
        var sectionIdResult = SectionId.Create(command.SectionId);
        if (sectionIdResult.IsFailure)
            return sectionIdResult.Error.ToErrors();

        var nameResult = FolderName.Create(command.Name);
        if (nameResult.IsFailure)
            return nameResult.Error.ToErrors();

        var rootResult = await containersRepository.GetRootBySectionIdAsync(
            sectionIdResult.Value, cancellationToken);
        if (rootResult.IsFailure)
            return rootResult.Error.ToErrors();

        LibraryContainer? root = rootResult.Value;
        if (root is null)
            return CommonErrors.NotFound(
                "library.container.root.not.found", "Корневой контейнер раздела не найден").ToErrors();

        var duplicateResult = await containersRepository.ExistsAsync(
            folder => folder.ParentId == root.Id && folder.Name == nameResult.Value,
            cancellationToken);
        if (duplicateResult.IsFailure)
            return duplicateResult.Error.ToErrors();
        if (duplicateResult.Value)
            return CommonErrors.Conflict(
                "library.container.folder.name.already.exists",
                "В разделе уже есть папка с таким названием").ToErrors();

        var folderResult = LibraryContainer.CreateFolder(
            root, nameResult.Value, command.Color, command.Icon);
        if (folderResult.IsFailure)
            return folderResult.Error.ToErrors();

        LibraryContainer folder = folderResult.Value;
        containersRepository.Add(folder);

        var saveResult = await transactionManager.SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToErrors();

        if (logger.IsEnabled(LogLevel.Information))
        {
            InfoLog.SectionFolderCreated(logger, folder.Id.Value, command.SectionId);
        }
        return folder.Id.Value;
    }
}
