using FluentValidation;

namespace Mnemora.Application.Library.GetHierarchySectionsPage;

public sealed class GetLibraryHierarchySectionsPageQueryValidator
    : AbstractValidator<GetLibraryHierarchySectionsPageQuery>
{
    public GetLibraryHierarchySectionsPageQueryValidator()
    {
        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
