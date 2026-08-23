using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Library.GetFoldersPage;

public sealed class GetLibraryFoldersPageQueryValidator
    : AbstractValidator<GetLibraryFoldersPageQuery>
{
    public GetLibraryFoldersPageQueryValidator()
    {
        RuleFor(query => query.ContainerId)
            .MustBeValueObject(LibraryContainerId.Create);

        RuleFor(query => query.Search)
            .Must(search =>
                string.IsNullOrWhiteSpace(search) ||
                search.Trim().Length <= FolderName.MAXLENGTH)
            .WithErrorCode("library.folders.search.too.long")
            .WithMessage(
                $"Поисковый запрос не должен превышать {FolderName.MAXLENGTH} символов.");

        RuleFor(query => query.Sort)
            .IsInEnum();

        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
