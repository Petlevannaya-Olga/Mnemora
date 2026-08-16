using FluentValidation;

namespace Mnemora.Application.Library.Order;

public sealed class SaveLibraryOrderCommandValidator : AbstractValidator<SaveLibraryOrderCommand>
{
    public SaveLibraryOrderCommandValidator()
    {
        RuleFor(command => command.Target).IsInEnum();
        RuleFor(command => command.OrderedIds).NotNull();
        RuleForEach(command => command.OrderedIds).NotEmpty();
        RuleFor(command => command.OrderedIds)
            .Must(ids => ids is not null && ids.Count == ids.Distinct().Count())
            .WithMessage("Список порядка содержит повторяющиеся элементы.");
        RuleFor(command => command.ParentId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .When(command => command.Target is not LibraryOrderTarget.Sections);
        RuleFor(command => command.ParentId)
            .Null()
            .When(command => command.Target is LibraryOrderTarget.Sections);
    }
}
