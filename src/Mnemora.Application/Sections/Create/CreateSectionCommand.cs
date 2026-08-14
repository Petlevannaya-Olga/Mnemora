
using Mnemora.Domain.Sections;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Create;

public sealed record CreateSectionCommand(
    string Name,
    SectionColor Color,
    SectionIcon Icon)
    : ICommandValidation;