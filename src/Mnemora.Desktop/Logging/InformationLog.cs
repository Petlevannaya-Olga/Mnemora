global using InfoLog = Mnemora.Desktop.Logging.InformationLog;

using Microsoft.Extensions.Logging;

namespace Mnemora.Desktop.Logging;

internal static partial class InformationLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress seed {Version} started")]
    internal static partial void StressSeedStarted(
        ILogger logger,
        string version);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress seed {Version} completed in {Elapsed}")]
    internal static partial void StressSeedCompleted(
        ILogger logger,
        string version,
        TimeSpan elapsed);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress library seed started")]
    internal static partial void StressLibrarySeedStarted(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress library seed completed")]
    internal static partial void StressLibrarySeedCompleted(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress V5 sections already exist: {Count}")]
    internal static partial void StressSectionsAlreadyExist(
        ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress V5 sections + roots: {Done}/{Total}")]
    internal static partial void StressSectionsBatchCompleted(
        ILogger logger,
        int done,
        int total);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress V5 {Label} already exist: {Count}")]
    internal static partial void StressItemsAlreadyExist(
        ILogger logger,
        string label,
        int count);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress V5 {Label}: {Done}/{Total}")]
    internal static partial void StressItemsBatchCompleted(
        ILogger logger,
        string label,
        int done,
        int total);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Открываем контейнер библиотеки {ContainerId}")]
    internal static partial void LibraryContainerOpening(
        ILogger logger,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress V5 linked questions already exist: {Count}")]
    internal static partial void StressLinkedQuestionsAlreadyExist(
        ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Stress V5 linked questions: {Done}/{Total}")]
    internal static partial void StressLinkedQuestionsBatchCompleted(
        ILogger logger,
        int done,
        int total);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Контейнер {ContainerId} найден: {FoldersCount} папок, {MaterialsCount} материалов")]
    internal static partial void LibraryContainerLoaded(
        ILogger logger,
        Guid containerId,
        int foldersCount,
        int materialsCount);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Материалы контейнера {ContainerId}: загружено {ItemsCount}, всего {TotalCount}, offset {Offset}")]
    internal static partial void LibraryContainerMaterialsLoaded(
        ILogger logger,
        Guid containerId,
        int itemsCount,
        int totalCount,
        int offset);
}
