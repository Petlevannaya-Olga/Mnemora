using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.LibraryContainers.CreateSectionFolder;

// UI creates a first-level folder within a section; nested folders use CreateLibraryFolderCommand.
public sealed record CreateSectionFolderCommand(
    Guid SectionId,
    string Name,
    FolderColor Color,
    FolderIcon Icon) : ICommandValidation;
