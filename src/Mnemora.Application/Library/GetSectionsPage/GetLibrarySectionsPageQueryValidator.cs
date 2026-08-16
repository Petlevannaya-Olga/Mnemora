using FluentValidation;
using Mnemora.Domain.Sections;

namespace Mnemora.Application.Library.GetSectionsPage;

public sealed class GetLibrarySectionsPageQueryValidator
    : AbstractValidator<GetLibrarySectionsPageQuery>
{
    public GetLibrarySectionsPageQueryValidator()
    {
        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Search)
            .MaximumLength(SectionName.MAXLENGTH)
            .When(query => !string.IsNullOrWhiteSpace(query.Search));

        RuleFor(query => query.Sort)
            .IsInEnum();
    }
}