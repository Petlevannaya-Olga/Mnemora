using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.LibraryContainers.Update;

public sealed class UpdateLibraryFolderCommandValidator : AbstractValidator<UpdateLibraryFolderCommand>
{
    public UpdateLibraryFolderCommandValidator()
    {
        RuleFor(x => x.FolderId).MustBeValueObject(LibraryContainerId.Create);
        RuleFor(x => x.Name).MustBeValueObject(FolderName.Create);
        RuleFor(x => x.Color).IsInEnum().WithError(CommonErrors.Validation(
            "library.folder.color.invalid", "Выбран некорректный цвет папки", nameof(UpdateLibraryFolderCommand.Color)));
        RuleFor(x => x.Icon).IsInEnum().WithError(CommonErrors.Validation(
            "library.folder.icon.invalid", "Выбрана некорректная иконка папки", nameof(UpdateLibraryFolderCommand.Icon)));
    }
}
