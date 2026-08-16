using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Order;

public sealed record SaveLibraryOrderCommand(
    LibraryOrderTarget Target,
    Guid? ParentId,
    IReadOnlyList<Guid> OrderedIds)
    : ICommandValidation;
