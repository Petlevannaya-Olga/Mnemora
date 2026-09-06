using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Library.GetContainerContents;

public sealed class GetLibraryContainerContentsQueryValidator
    : AbstractValidator<GetLibraryContainerContentsQuery>
{
    public GetLibraryContainerContentsQueryValidator()
    {
        RuleFor(query => query.ContainerId)
            .MustBeValueObject(LibraryContainerId.Create);
    }
}
