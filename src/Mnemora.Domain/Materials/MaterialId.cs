using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed record MaterialId
{
    public Guid Value { get; }

    private MaterialId(Guid value)
    {
        Value = value;
    }

    public static MaterialId New()
    {
        return new MaterialId(Guid.NewGuid());
    }

    public static Result<MaterialId, Error> Create(Guid materialId)
    {
        if (materialId == Guid.Empty)
        {
            return CommonErrors.IsEmpty(nameof(materialId));
        }

        return new MaterialId(materialId);
    }
}