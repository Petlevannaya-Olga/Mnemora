
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Create;

public sealed record CreateSectionCommand(string Name) : ICommandValidation;