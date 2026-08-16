using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Order;

public sealed record GetLibraryOrderItemsQuery(
    LibraryOrderTarget Target,
    Guid? ParentId)
    : IQuery;
