using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Library.GetTopicsPage;

public sealed class GetLibraryTopicsPageQueryValidator : AbstractValidator<GetLibraryTopicsPageQuery>
{
    public GetLibraryTopicsPageQueryValidator()
    {
        RuleFor(query => query.SectionId)
            .MustBeValueObject(SectionId.Create);

        RuleFor(query => query.Search)
            .Must(search =>
                string.IsNullOrWhiteSpace(search) ||
                search.Trim().Length <= TopicName.MAXLENGTH)
            .WithErrorCode("library.topics.search.too.long")
            .WithMessage($"Поисковый запрос не должен превышать {TopicName.MAXLENGTH} символов.");

        RuleFor(query => query.Sort)
            .IsInEnum();

        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}