using FluentValidation;
using Mnemora.Domain.Materials;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Materials.GetDetails;

public sealed class GetMaterialDetailsQueryValidator : AbstractValidator<GetMaterialDetailsQuery>
{
    public GetMaterialDetailsQueryValidator()
    {
        RuleFor(query => query.MaterialId).MustBeValueObject(MaterialId.Create);
    }
}