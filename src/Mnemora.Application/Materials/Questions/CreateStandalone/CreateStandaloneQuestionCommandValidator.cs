using FluentValidation;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Materials.Questions.CreateStandalone;

public sealed class CreateStandaloneQuestionCommandValidator
    : AbstractValidator<CreateStandaloneQuestionCommand>
{
    public CreateStandaloneQuestionCommandValidator()
    {
        RuleFor(command => command.TopicId).MustBeValueObject(TopicId.Create);
        RuleFor(command => command.Title).MustBeValueObject(MaterialTitle.Create);

        RuleFor(command => command.Difficulty)
            .IsInEnum()
            .WithMessage("Указана недопустимая сложность материала.");

        RuleFor(command => command.IconKey)
            .MustBeValueObject(MaterialIcon.Create)
            .When(command => command.IconKey is not null);

        RuleFor(command => command.StudyPoints)
            .InclusiveBetween(MaterialExperienceRewards.MinPoints, MaterialExperienceRewards.MaxPoints);

        RuleFor(command => command.ReviewPoints)
            .InclusiveBetween(MaterialExperienceRewards.MinPoints, MaterialExperienceRewards.MaxPoints);

        RuleFor(command => command.ReviewPoints)
            .LessThan(command => command.StudyPoints)
            .WithMessage("Награда за повторение должна быть меньше награды за первичное изучение.");

        RuleFor(command => command.PromptMarkdown)
            .NotEmpty()
            .WithMessage("Текст вопроса обязателен.");

        RuleFor(command => command.ReferenceAnswerMarkdown)
            .NotEmpty()
            .WithMessage("Эталонный ответ обязателен.");

        RuleFor(command => command.Tags)
            .Must(tags => tags is null || tags.Count <= Material.MaxTags)
            .WithMessage($"Материал может содержать не более {Material.MaxTags} тегов.");

        RuleFor(command => command.Tags)
            .Must(TagsAreUnique)
            .WithMessage("Материал не может содержать повторяющиеся теги.");

        RuleForEach(command => command.Tags!)
            .MustBeValueObject(MaterialTag.Create)
            .When(command => command.Tags is not null);
    }

    private static bool TagsAreUnique(IReadOnlyCollection<string>? tags)
    {
        if (tags is null)
        {
            return true;
        }

        var uniqueTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !uniqueTags.Add(tag.Trim()))
            {
                return false;
            }
        }

        return true;
    }
}