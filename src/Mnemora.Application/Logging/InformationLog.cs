global using InfoLog = Mnemora.Application.Logging.InformationLog;

using Microsoft.Extensions.Logging;

namespace Mnemora.Application.Logging;

internal static partial class InformationLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение разделов было отменено")]
    internal static partial void SectionsRetrievalCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создан раздел {SectionId} с названием {SectionName} и корневым контейнером {ContainerId}")]
    internal static partial void SectionCreated(
        ILogger logger,
        Guid sectionId,
        string sectionName,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создана папка {FolderId} в разделе {SectionId}")]
    internal static partial void SectionFolderCreated(
        ILogger logger,
        Guid folderId,
        Guid sectionId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Обновлена папка {FolderId}")]
    internal static partial void LibraryFolderUpdated(
        ILogger logger,
        Guid folderId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Обновлён раздел {SectionId} с названием {SectionName}")]
    internal static partial void SectionUpdated(
        ILogger logger,
        Guid sectionId,
        string sectionName);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Удалена пустая папка {FolderId}")]
    internal static partial void EmptyLibraryFolderDeleted(
        ILogger logger,
        Guid folderId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Сохранён ручной порядок {Target}. Элементов: {Count}")]
    internal static partial void LibraryOrderSaved(
        ILogger logger,
        global::Mnemora.Application.Library.Order.LibraryOrderTarget target,
        int count);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Удалена статья {ArticleId} с названием {ArticleTitle}")]
    internal static partial void ArticleDeleted(
        ILogger logger,
        Guid articleId,
        string articleTitle);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создана папка библиотеки {FolderId} в контейнере {ParentContainerId}")]
    internal static partial void LibraryFolderCreated(
        ILogger logger,
        Guid folderId,
        Guid parentContainerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Удалён раздел {SectionId} с названием {SectionName}")]
    internal static partial void SectionDeleted(
        ILogger logger,
        Guid sectionId,
        string sectionName);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создана статья {ArticleId} с названием {ArticleTitle} в контейнере {ContainerId}")]
    internal static partial void ArticleCreated(
        ILogger logger,
        Guid articleId,
        string articleTitle,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Изменена статья {ArticleId}. Контейнер: {ContainerId}. Перемещено связанных вопросов: {QuestionCount}")]
    internal static partial void ArticleUpdated(
        ILogger logger,
        Guid articleId,
        Guid containerId,
        int questionCount);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создана статья {ArticleId}. Новых вопросов: {NewCount}, прикреплено готовых: {ExistingCount}")]
    internal static partial void MaterialGraphArticleCreated(
        ILogger logger,
        Guid articleId,
        int newCount,
        int existingCount);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создан вопрос {QuestionId}, связанный со статьёй {ArticleId}")]
    internal static partial void ArticleQuestionCreated(
        ILogger logger,
        Guid questionId,
        Guid articleId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение сводки главной страницы было отменено")]
    internal static partial void HomeSummaryRetrievalCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Удалён вопрос {QuestionId} с названием {QuestionTitle}")]
    internal static partial void QuestionDeleted(
        ILogger logger,
        Guid questionId,
        string questionTitle);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение содержимого контейнера {ContainerId} было отменено")]
    internal static partial void ContainerContentsRetrievalCancelled(
        ILogger logger,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создан вопрос {QuestionId}. Статья: {ArticleId}")]
    internal static partial void MaterialGraphQuestionCreated(
        ILogger logger,
        Guid questionId,
        Guid? articleId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение материала {MaterialId} было отменено")]
    internal static partial void MaterialDetailsRetrievalCancelled(
        ILogger logger,
        Guid materialId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Загрузка самостоятельных вопросов для выбора была отменена")]
    internal static partial void StandaloneQuestionPickerCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Создан самостоятельный вопрос {QuestionId} с названием {QuestionTitle} в контейнере {ContainerId}")]
    internal static partial void StandaloneQuestionCreated(
        ILogger logger,
        Guid questionId,
        string questionTitle,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Загрузка вариантов связей для контейнера {ContainerId} была отменена")]
    internal static partial void LearningLinkOptionsCancelled(
        ILogger logger,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Изменён вопрос {QuestionId}. Контейнер: {ContainerId}. Статья: {ArticleId}")]
    internal static partial void QuestionUpdated(
        ILogger logger,
        Guid questionId,
        Guid containerId,
        Guid? articleId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы разделов дерева было отменено")]
    internal static partial void HierarchySectionsPageCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы материалов контейнера {ContainerId} было отменено")]
    internal static partial void MaterialsPageCancelled(
        ILogger logger,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы папок дерева для контейнера {ContainerId} было отменено")]
    internal static partial void HierarchyFoldersPageCancelled(
        ILogger logger,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы папок контейнера {ContainerId} было отменено")]
    internal static partial void FoldersPageCancelled(
        ILogger logger,
        Guid containerId);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение библиотеки было отменено")]
    internal static partial void LibraryRetrievalCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы папок управления было отменено")]
    internal static partial void ManagementFoldersPageCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы разделов было отменено")]
    internal static partial void SectionsPageCancelled(
        ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Получение страницы разделов управления было отменено")]
    internal static partial void ManagementSectionsPageCancelled(
        ILogger logger);
}
