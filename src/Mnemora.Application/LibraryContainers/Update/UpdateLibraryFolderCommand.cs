using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.Update;

public sealed record UpdateLibraryFolderCommand(
    Guid FolderId,
    string Name,
    FolderColor Color,
    FolderIcon Icon) : ICommandValidation;
