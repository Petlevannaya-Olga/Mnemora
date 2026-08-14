using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Delete;

public sealed record DeleteSectionCommand(Guid SectionId) : ICommandValidation;