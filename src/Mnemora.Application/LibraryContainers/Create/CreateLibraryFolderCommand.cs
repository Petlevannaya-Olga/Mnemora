using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.Create;

public sealed record CreateLibraryFolderCommand(
    Guid ParentContainerId,
    string Name,
    FolderColor Color,
    FolderIcon Icon)
    : ICommandValidation;
