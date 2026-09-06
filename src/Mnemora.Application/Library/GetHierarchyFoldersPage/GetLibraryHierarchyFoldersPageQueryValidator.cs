using FluentValidation;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Library.GetHierarchyFoldersPage;

public sealed class GetLibraryHierarchyFoldersPageQueryValidator
    : AbstractValidator<GetLibraryHierarchyFoldersPageQuery>
{
    public GetLibraryHierarchyFoldersPageQueryValidator()
    {
        RuleFor(query => query.ContainerId)
            .MustBeValueObject(LibraryContainerId.Create);

        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
