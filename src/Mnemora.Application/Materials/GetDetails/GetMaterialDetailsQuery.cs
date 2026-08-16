using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.GetDetails;

public sealed record GetMaterialDetailsQuery(Guid MaterialId) : IQuery;