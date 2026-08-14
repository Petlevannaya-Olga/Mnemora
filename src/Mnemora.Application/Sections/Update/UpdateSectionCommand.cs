using Mnemora.Domain.Sections;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Update;

public sealed record UpdateSectionCommand(
    Guid SectionId,
    string Name,
    SectionColor Color,
    SectionIcon Icon)
    : ICommandValidation;