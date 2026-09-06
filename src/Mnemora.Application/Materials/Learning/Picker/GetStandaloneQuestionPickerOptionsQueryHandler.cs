using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Learning.Picker;

public sealed class GetStandaloneQuestionPickerOptionsQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetStandaloneQuestionPickerOptionsQueryHandler> logger)
    : IQueryHandler<
        IReadOnlyList<StandaloneQuestionPickerOptionDto>,
        GetStandaloneQuestionPickerOptionsQuery>
{
    public async Task<Result<IReadOnlyList<StandaloneQuestionPickerOptionDto>, Errors>> Handle(
        GetStandaloneQuestionPickerOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<Question> questions = await readDbContext.MaterialsRead
                .OfType<Question>()
                .Where(question => question.ArticleId == null)
                .ToListAsync(cancellationToken);

            if (questions.Count == 0)
            {
                return Result.Success<
                    IReadOnlyList<StandaloneQuestionPickerOptionDto>,
                    Errors>(Array.Empty<StandaloneQuestionPickerOptionDto>());
            }

            List<LibraryContainer> containers = await readDbContext.LibraryContainersRead
                .ToListAsync(cancellationToken);

            var sections = await readDbContext.SectionsRead
                .ToListAsync(cancellationToken);

            var containersById = containers.ToDictionary(container => container.Id.Value);
            var sectionsById = sections.ToDictionary(section => section.Id.Value);
            var result = new List<StandaloneQuestionPickerOptionDto>(questions.Count);

            foreach (Question question in questions)
            {
                if (!containersById.TryGetValue(question.ContainerId.Value, out LibraryContainer? container) ||
                    !sectionsById.TryGetValue(container.SectionId.Value, out var section))
                {
                    continue;
                }

                result.Add(
                    new StandaloneQuestionPickerOptionDto(
                        question.Id.Value,
                        question.Title.Value,
                        question.Difficulty.ToString(),
                        question.ExperienceRewards.StudyPoints,
                        question.ExperienceRewards.ReviewPoints,
                        container.Id.Value,
                        BuildContainerName(container, containersById),
                        section.Id.Value,
                        section.Name.Value));
            }

            IReadOnlyList<StandaloneQuestionPickerOptionDto> ordered = result
                .OrderBy(option => option.SectionName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.TopicName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Result.Success<
                IReadOnlyList<StandaloneQuestionPickerOptionDto>,
                Errors>(ordered);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Загрузка самостоятельных вопросов для выбора была отменена");

            return CommonErrors.OperationCancelled(
                    "material.question.picker.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось загрузить самостоятельные вопросы для выбора");

            return CommonErrors.Db(
                    "material.question.picker.failed",
                    "Не удалось загрузить самостоятельные вопросы")
                .ToErrors();
        }
    }

    private static string BuildContainerName(
        LibraryContainer container,
        IReadOnlyDictionary<Guid, LibraryContainer> containersById)
    {
        if (container.IsRoot)
        {
            return "Без папки";
        }

        var names = new Stack<string>();
        LibraryContainer? current = container;

        while (current is not null && !current.IsRoot)
        {
            if (current.Name is not null)
            {
                names.Push(current.Name.Value);
            }

            if (current.ParentId is null ||
                !containersById.TryGetValue(current.ParentId.Value, out current))
            {
                break;
            }
        }

        return names.Count == 0
            ? "Без папки"
            : string.Join(" / ", names);
    }
}
