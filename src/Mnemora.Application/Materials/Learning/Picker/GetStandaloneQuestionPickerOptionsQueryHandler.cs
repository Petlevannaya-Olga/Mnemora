using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
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
            // Загружаем только самостоятельные вопросы. Разделы и темы нужны
            // исключительно как навигационный контекст в окне выбора.
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

            var topics = await readDbContext.TopicsRead
                .ToListAsync(cancellationToken);

            var sections = await readDbContext.SectionsRead
                .ToListAsync(cancellationToken);

            var topicsById = topics.ToDictionary(topic => topic.Id.Value);
            var sectionsById = sections.ToDictionary(section => section.Id.Value);

            var result = new List<StandaloneQuestionPickerOptionDto>(questions.Count);

            foreach (Question question in questions)
            {
                if (!topicsById.TryGetValue(question.TopicId.Value, out var topic) ||
                    !sectionsById.TryGetValue(topic.SectionId.Value, out var section))
                {
                    // При нарушенной ссылочной целостности не показываем элемент
                    // в picker: пользователь всё равно не сможет корректно понять,
                    // откуда он будет перенесён.
                    continue;
                }

                result.Add(
                    new StandaloneQuestionPickerOptionDto(
                        question.Id.Value,
                        question.Title.Value,
                        question.Difficulty.ToString(),
                        question.ExperienceRewards.StudyPoints,
                        question.ExperienceRewards.ReviewPoints,
                        topic.Id.Value,
                        topic.Name.Value,
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
}
