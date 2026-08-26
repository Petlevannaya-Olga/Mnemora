using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.LibraryContainers.Create;

public sealed class CreateLibraryFolderCommandValidator
    : AbstractValidator<CreateLibraryFolderCommand>
{
    public CreateLibraryFolderCommandValidator()
    {
        RuleFor(command => command.ParentContainerId)
            .MustBeValueObject(LibraryContainerId.Create);

        RuleFor(command => command.Name)
            .MustBeValueObject(FolderName.Create);

        RuleFor(command => command.Color)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "library.container.folder.color.is.invalid",
                "Выбран некорректный цвет папки",
                nameof(CreateLibraryFolderCommand.Color)));

        RuleFor(command => command.Icon)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "library.container.folder.icon.is.invalid",
                "Выбрана некорректная иконка папки",
                nameof(CreateLibraryFolderCommand.Icon)));
    }
}
