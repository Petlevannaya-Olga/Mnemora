global using InfoLog = Mnemora.Infrastructure.Logging.InformationLog;

using Microsoft.Extensions.Logging;

namespace Mnemora.Infrastructure.Logging;

internal static partial class InformationLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Фиксация транзакции была отменена")]
    internal static partial void TransactionCommitCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создание транзакции было отменено")]
    internal static partial void TransactionStartCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Сохранение изменений было отменено")]
    internal static partial void SaveChangesCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Откат транзакции был отменён")]
    internal static partial void TransactionRollbackCancelled(
        ILogger logger);
}
