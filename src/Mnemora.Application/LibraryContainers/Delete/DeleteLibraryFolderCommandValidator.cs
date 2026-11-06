using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.LibraryContainers.Delete;

public sealed class DeleteLibraryFolderCommandValidator : AbstractValidator<DeleteLibraryFolderCommand>
{
    public DeleteLibraryFolderCommandValidator()
    {
        RuleFor(x => x.FolderId).MustBeValueObject(LibraryContainerId.Create);
    }
}
