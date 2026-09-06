using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;
using ArticleMaterial = Mnemora.Domain.Materials.Article;

namespace Mnemora.Application.Materials.CreateGraph;

/// <summary>
/// Создаёт весь результат мастера одной логической операцией.
///
/// Markdown хранится вне SQLite, поэтому полной ACID-транзакции между БД и
/// файловой системой быть не может. Сначала создаём все новые Markdown-файлы,
/// затем одной короткой транзакцией сохраняем весь граф в БД. При любой ошибке
/// новые файлы компенсирующе удаляются.
/// </summary>
public sealed class CreateMaterialGraphCommandHandler(
    ILibraryContainersRepository libraryContainersRepository,
    IMaterialsRepository materialsRepository,
    IMaterialContentStore materialContentStore,
    ITransactionManager transactionManager,
    ILogger<CreateMaterialGraphCommandHandler> logger)
    : ICommandHandler<Guid, CreateMaterialGraphCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateMaterialGraphCommand command,
        CancellationToken cancellationToken)
    {
        var createdContent = new List<CreatedContent>();

        try
        {
            return command.Type switch
            {
                MaterialType.Article => await CreateArticleGraphAsync(
                    command,
                    createdContent,
                    cancellationToken),

                MaterialType.Question => await CreateQuestionAsync(
                    command,
                    createdContent,
                    cancellationToken),

                _ => CommonErrors.Validation(
                        "material.type.invalid",
                        "Указан неподдерживаемый тип материала.",
                        nameof(command.Type))
                    .ToErrors(),
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            CleanupCreatedContent(createdContent);

            return CommonErrors.OperationCancelled(
                    "material.graph.create.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            CleanupCreatedContent(createdContent);

            logger.LogError(
                exception,
                "Не удалось завершить создание материала и его связей");

            return CommonErrors.Failure(
                    "material.graph.create.failed",
                    "Не удалось создать материал. Изменения не сохранены.")
                .ToErrors();
        }
    }

    private async Task<Result<Guid, Errors>> CreateArticleGraphAsync(
        CreateMaterialGraphCommand command,
        List<CreatedContent> createdContent,
        CancellationToken cancellationToken)
    {
        var commonResult = await PrepareCommonAsync(
            command.ContainerId,
            command.Title,
            command.Difficulty,
            command.IconKey,
            command.StudyPoints,
            command.ReviewPoints,
            command.Tags,
            cancellationToken);

        if (commonResult.IsFailure)
        {
            return commonResult.Error;
        }

        var articleContentResult =
            ArticleContent.Create(command.BodyMarkdown);

        if (articleContentResult.IsFailure)
        {
            return articleContentResult.Error.ToErrors();
        }

        var articleResult = ArticleMaterial.CreateInContainer(
            commonResult.Value.ContainerId,
            commonResult.Value.Title,
            command.Difficulty,
            commonResult.Value.Icon,
            commonResult.Value.Rewards,
            commonResult.Value.Tags);

        if (articleResult.IsFailure)
        {
            return articleResult.Error.ToErrors();
        }

        ArticleMaterial article = articleResult.Value;

        // Сначала проверяем все выбранные готовые вопросы. Никаких файлов и
        // SaveChanges до окончания этой проверки ещё нет.
        var existingQuestionsResult = await LoadStandaloneQuestionsAsync(
            command.ExistingQuestionIds,
            cancellationToken);

        if (existingQuestionsResult.IsFailure)
        {
            return existingQuestionsResult.Error;
        }

        var newQuestionsResult = PrepareNewQuestions(
            article,
            command.NewQuestions);

        if (newQuestionsResult.IsFailure)
        {
            return newQuestionsResult.Error;
        }

        // Готовый вопрос после привязки больше не имеет собственных тегов.
        // Его эффективные теги читаются через статью.
        foreach (Question question in existingQuestionsResult.Value)
        {
            var attachResult = question.AttachToArticle(article);
            if (attachResult.IsFailure)
            {
                return attachResult.Error.ToErrors();
            }

            var clearTagsResult = question.ReplaceTags(
                Array.Empty<MaterialTag>());

            if (clearTagsResult.IsFailure)
            {
                return clearTagsResult.Error.ToErrors();
            }
        }

        var articleFileResult = await materialContentStore.CreateArticleAsync(
            article.Id,
            articleContentResult.Value,
            cancellationToken);

        if (articleFileResult.IsFailure)
        {
            return articleFileResult.Error.ToErrors();
        }

        createdContent.Add(new CreatedContent(article.Id, MaterialType.Article));

        foreach (PreparedQuestion prepared in newQuestionsResult.Value)
        {
            var questionFileResult =
                await materialContentStore.CreateQuestionAsync(
                    prepared.Question.Id,
                    prepared.Content,
                    cancellationToken);

            if (questionFileResult.IsFailure)
            {
                CleanupCreatedContent(createdContent);
                createdContent.Clear();
                return questionFileResult.Error.ToErrors();
            }

            createdContent.Add(
                new CreatedContent(
                    prepared.Question.Id,
                    MaterialType.Question));
        }

        materialsRepository.Add(article);
        foreach (PreparedQuestion prepared in newQuestionsResult.Value)
        {
            materialsRepository.Add(prepared.Question);
        }

        var persistResult = await PersistAtomicallyAsync(
            createdContent,
            cancellationToken);

        if (persistResult.IsFailure)
        {
            return persistResult.Error;
        }

        logger.LogInformation(
            "Создана статья {ArticleId}. Новых вопросов: {NewCount}, прикреплено готовых: {ExistingCount}",
            article.Id.Value,
            newQuestionsResult.Value.Count,
            existingQuestionsResult.Value.Count);

        return article.Id.Value;
    }

    private async Task<Result<Guid, Errors>> CreateQuestionAsync(
        CreateMaterialGraphCommand command,
        List<CreatedContent> createdContent,
        CancellationToken cancellationToken)
    {
        var titleResult = MaterialTitle.Create(command.Title);
        if (titleResult.IsFailure)
        {
            return titleResult.Error.ToErrors();
        }

        var iconResult = CreateIcon(command.IconKey);
        if (iconResult.IsFailure)
        {
            return iconResult.Error.ToErrors();
        }

        var rewardsResult = MaterialExperienceRewards.Create(
            command.StudyPoints,
            command.ReviewPoints);

        if (rewardsResult.IsFailure)
        {
            return rewardsResult.Error.ToErrors();
        }

        var contentResult = QuestionContent.Create(
            command.BodyMarkdown,
            command.ReferenceAnswerMarkdown);

        if (contentResult.IsFailure)
        {
            return contentResult.Error.ToErrors();
        }

        Question question;

        if (command.ArticleId is Guid articleGuid)
        {
            var articleIdResult = MaterialId.Create(articleGuid);
            if (articleIdResult.IsFailure)
            {
                return articleIdResult.Error.ToErrors();
            }

            var materialResult = await materialsRepository.GetByIdAsync(
                articleIdResult.Value,
                cancellationToken);

            if (materialResult.IsFailure)
            {
                return materialResult.Error.ToErrors();
            }

            if (materialResult.Value is not ArticleMaterial article)
            {
                return CommonErrors.Validation(
                        "article.not.found",
                        "Выбранная статья больше не существует.",
                        nameof(command.ArticleId))
                    .ToErrors();
            }

            // У связанного вопроса собственных тегов нет.
            var questionResult = Question.CreateForArticle(
                article,
                titleResult.Value,
                command.Difficulty,
                iconResult.Value,
                rewardsResult.Value,
                Array.Empty<MaterialTag>());

            if (questionResult.IsFailure)
            {
                return questionResult.Error.ToErrors();
            }

            question = questionResult.Value;
        }
        else
        {
            var containerResult = await EnsureContainerExistsAsync(
                command.ContainerId,
                cancellationToken);

            if (containerResult.IsFailure)
            {
                return containerResult.Error;
            }

            var tagsResult = CreateTags(command.Tags);
            if (tagsResult.IsFailure)
            {
                return tagsResult.Error.ToErrors();
            }

            var questionResult = Question.CreateStandaloneInContainer(
                containerResult.Value,
                titleResult.Value,
                command.Difficulty,
                iconResult.Value,
                rewardsResult.Value,
                tagsResult.Value);

            if (questionResult.IsFailure)
            {
                return questionResult.Error.ToErrors();
            }

            question = questionResult.Value;
        }

        var fileResult = await materialContentStore.CreateQuestionAsync(
            question.Id,
            contentResult.Value,
            cancellationToken);

        if (fileResult.IsFailure)
        {
            return fileResult.Error.ToErrors();
        }

        createdContent.Add(new CreatedContent(question.Id, MaterialType.Question));
        materialsRepository.Add(question);

        var persistResult = await PersistAtomicallyAsync(
            createdContent,
            cancellationToken);

        if (persistResult.IsFailure)
        {
            return persistResult.Error;
        }

        logger.LogInformation(
            "Создан вопрос {QuestionId}. Статья: {ArticleId}",
            question.Id.Value,
            question.ArticleId?.Value);

        return question.Id.Value;
    }

    private async Task<Result<PreparedCommon, Errors>> PrepareCommonAsync(
        Guid containerId,
        string title,
        MaterialDifficulty difficulty,
        string? iconKey,
        int studyPoints,
        int reviewPoints,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken)
    {
        var actualContainerResult = await EnsureContainerExistsAsync(
            containerId,
            cancellationToken);

        if (actualContainerResult.IsFailure)
        {
            return actualContainerResult.Error;
        }

        var titleResult = MaterialTitle.Create(title);
        if (titleResult.IsFailure)
        {
            return titleResult.Error.ToErrors();
        }

        var iconResult = CreateIcon(iconKey);
        if (iconResult.IsFailure)
        {
            return iconResult.Error.ToErrors();
        }

        var rewardsResult = MaterialExperienceRewards.Create(
            studyPoints,
            reviewPoints);

        if (rewardsResult.IsFailure)
        {
            return rewardsResult.Error.ToErrors();
        }

        var tagsResult = CreateTags(tags);
        if (tagsResult.IsFailure)
        {
            return tagsResult.Error.ToErrors();
        }

        return new PreparedCommon(
            actualContainerResult.Value,
            titleResult.Value,
            iconResult.Value,
            rewardsResult.Value,
            tagsResult.Value);
    }

    private async Task<Result<LibraryContainerId, Errors>> EnsureContainerExistsAsync(
        Guid containerId,
        CancellationToken cancellationToken)
    {
        var containerIdResult = LibraryContainerId.Create(containerId);
        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
        }

        var containerResult = await libraryContainersRepository.GetByIdAsync(
            containerIdResult.Value,
            cancellationToken);

        if (containerResult.IsFailure)
        {
            return containerResult.Error.ToErrors();
        }

        if (containerResult.Value is null)
        {
            return CommonErrors.NotFound(
                    "library.container.not.found",
                    $"Контейнер библиотеки с идентификатором '{containerId}' не найден")
                .ToErrors();
        }

        return containerIdResult.Value;
    }

    private async Task<Result<IReadOnlyList<Question>, Errors>>
        LoadStandaloneQuestionsAsync(
            IReadOnlyCollection<Guid>? questionIds,
            CancellationToken cancellationToken)
    {
        if (questionIds is null || questionIds.Count == 0)
        {
            return Array.Empty<Question>();
        }

        var questions = new List<Question>();

        foreach (Guid id in questionIds.Distinct())
        {
            var materialIdResult = MaterialId.Create(id);
            if (materialIdResult.IsFailure)
            {
                return materialIdResult.Error.ToErrors();
            }

            var materialResult = await materialsRepository.GetByIdAsync(
                materialIdResult.Value,
                cancellationToken);

            if (materialResult.IsFailure)
            {
                return materialResult.Error.ToErrors();
            }

            if (materialResult.Value is not Question question)
            {
                return CommonErrors.Validation(
                        "question.not.found",
                        "Один из выбранных вопросов больше не существует.",
                        nameof(questionIds))
                    .ToErrors();
            }

            if (question.ArticleId is not null)
            {
                return CommonErrors.Conflict(
                        "question.already.attached",
                        $"Вопрос «{question.Title.Value}» уже прикреплён к другой статье.")
                    .ToErrors();
            }

            questions.Add(question);
        }

        return questions;
    }

    private static Result<IReadOnlyList<PreparedQuestion>, Errors>
        PrepareNewQuestions(
            ArticleMaterial article,
            IReadOnlyCollection<CreateMaterialGraphQuestionDraft>? drafts)
    {
        if (drafts is null || drafts.Count == 0)
        {
            return Array.Empty<PreparedQuestion>();
        }

        var result = new List<PreparedQuestion>(drafts.Count);

        foreach (CreateMaterialGraphQuestionDraft draft in drafts)
        {
            var titleResult = MaterialTitle.Create(draft.Title);
            if (titleResult.IsFailure)
            {
                return titleResult.Error.ToErrors();
            }

            var iconResult = CreateIcon(draft.IconKey);
            if (iconResult.IsFailure)
            {
                return iconResult.Error.ToErrors();
            }

            var rewardsResult = MaterialExperienceRewards.Create(
                draft.StudyPoints,
                draft.ReviewPoints);

            if (rewardsResult.IsFailure)
            {
                return rewardsResult.Error.ToErrors();
            }

            var contentResult = QuestionContent.Create(
                draft.PromptMarkdown,
                draft.ReferenceAnswerMarkdown);

            if (contentResult.IsFailure)
            {
                return contentResult.Error.ToErrors();
            }

            var questionResult = Question.CreateForArticle(
                article,
                titleResult.Value,
                draft.Difficulty,
                iconResult.Value,
                rewardsResult.Value,
                Array.Empty<MaterialTag>());

            if (questionResult.IsFailure)
            {
                return questionResult.Error.ToErrors();
            }

            result.Add(
                new PreparedQuestion(
                    questionResult.Value,
                    contentResult.Value));
        }

        return result;
    }

    private async Task<UnitResult<Errors>> PersistAtomicallyAsync(
        List<CreatedContent> createdContent,
        CancellationToken cancellationToken)
    {
        // Все изменения БД находятся в одном DbContext и отправляются одним
        // SaveChangesAsync. Для SQLite/EF Core это одна атомарная DB-операция.
        // Файловая система в DB-транзакцию не входит, поэтому при неуспехе
        // компенсирующе удаляем только созданные этой командой каталоги.
        var saveResult =
            await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            CleanupCreatedContent(createdContent);
            createdContent.Clear();
            return UnitResult.Failure(saveResult.Error.ToErrors());
        }

        // После успешного SaveChanges данные в БД уже зафиксированы. Очищаем
        // список компенсации, чтобы неожиданная ошибка после сохранения не
        // удалила Markdown уже созданного материала.
        createdContent.Clear();

        return UnitResult.Success<Errors>();
    }

    private void CleanupCreatedContent(
        IEnumerable<CreatedContent> createdContent)
    {
        foreach (CreatedContent item in createdContent.Reverse())
        {
            var deleteResult = materialContentStore.Delete(
                item.Id,
                item.Type);

            if (deleteResult.IsFailure)
            {
                logger.LogError(
                    "Не удалось удалить Markdown-файлы материала {MaterialId} после отката. Код ошибки: {ErrorCode}",
                    item.Id.Value,
                    deleteResult.Error.Code);
            }
        }
    }

    private static Result<MaterialIcon?, Error> CreateIcon(string? iconKey)
    {
        if (iconKey is null)
        {
            return Result.Success<MaterialIcon?, Error>(null);
        }

        var result = MaterialIcon.Create(iconKey);
        return result.IsFailure
            ? result.Error
            : Result.Success<MaterialIcon?, Error>(result.Value);
    }

    private static Result<IReadOnlyCollection<MaterialTag>, Error> CreateTags(
        IReadOnlyCollection<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return Array.Empty<MaterialTag>();
        }

        var result = new List<MaterialTag>(tags.Count);

        foreach (string tag in tags)
        {
            var tagResult = MaterialTag.Create(tag);
            if (tagResult.IsFailure)
            {
                return tagResult.Error;
            }

            if (!result.Contains(tagResult.Value))
            {
                result.Add(tagResult.Value);
            }
        }

        return result;
    }

    private sealed record PreparedCommon(
        LibraryContainerId ContainerId,
        MaterialTitle Title,
        MaterialIcon? Icon,
        MaterialExperienceRewards Rewards,
        IReadOnlyCollection<MaterialTag> Tags);

    private sealed record PreparedQuestion(
        Question Question,
        QuestionContent Content);

    private sealed record CreatedContent(
        MaterialId Id,
        MaterialType Type);
}
