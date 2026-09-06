using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionRoot;

public sealed record GetLibrarySectionRootQuery(Guid SectionId) : IQueryValidation;
