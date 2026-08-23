using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Library.GetMaterialsPage;

public sealed class GetLibraryMaterialsPageQueryValidator : AbstractValidator<GetLibraryMaterialsPageQuery>
{
    public GetLibraryMaterialsPageQueryValidator()
    {
        RuleFor(query => query.ContainerId)
            .MustBeValueObject(LibraryContainerId.Create);

        RuleFor(query => query.Search)
            .Must(search =>
                string.IsNullOrWhiteSpace(search) ||
                search.Trim().Length <= MaterialTitle.MaxLength)
            .WithErrorCode("library.materials.search.too.long")
            .WithMessage($"Поисковый запрос не должен превышать {MaterialTitle.MaxLength} символов.");

        RuleFor(query => query.Filter)
            .IsInEnum();

        RuleFor(query => query.Sort)
            .IsInEnum();

        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
