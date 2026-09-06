using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Learning.GetOptions;

public sealed record GetMaterialLearningLinkOptionsQuery(
    Guid ContainerId) : IQuery;
