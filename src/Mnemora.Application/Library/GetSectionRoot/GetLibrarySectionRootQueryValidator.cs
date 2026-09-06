using FluentValidation;

namespace Mnemora.Application.Library.GetSectionRoot;

public sealed class GetLibrarySectionRootQueryValidator : AbstractValidator<GetLibrarySectionRootQuery>
{
    public GetLibrarySectionRootQueryValidator()
    {
        RuleFor(query => query.SectionId).NotEmpty();
    }
}
