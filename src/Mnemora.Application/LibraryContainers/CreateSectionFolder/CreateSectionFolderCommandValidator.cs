using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.LibraryContainers.CreateSectionFolder;

public sealed class CreateSectionFolderCommandValidator : AbstractValidator<CreateSectionFolderCommand>
{
    public CreateSectionFolderCommandValidator()
    {
        RuleFor(x => x.SectionId).MustBeValueObject(SectionId.Create);
        RuleFor(x => x.Name).MustBeValueObject(FolderName.Create);
        RuleFor(x => x.Color).IsInEnum().WithError(CommonErrors.Validation(
            "library.folder.color.invalid", "Выбран некорректный цвет папки", nameof(CreateSectionFolderCommand.Color)));
        RuleFor(x => x.Icon).IsInEnum().WithError(CommonErrors.Validation(
            "library.folder.icon.invalid", "Выбрана некорректная иконка папки", nameof(CreateSectionFolderCommand.Icon)));
    }
}
