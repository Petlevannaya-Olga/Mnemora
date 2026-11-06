using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.Delete;

public sealed record DeleteLibraryFolderCommand(Guid FolderId) : ICommandValidation;
