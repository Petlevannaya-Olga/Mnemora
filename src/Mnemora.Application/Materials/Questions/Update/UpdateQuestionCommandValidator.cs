using FluentValidation;
using Mnemora.Domain.Materials;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Materials.Questions.Update;

public sealed class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionCommandValidator()
    {
        RuleFor(command => command.QuestionId).MustBeValueObject(MaterialId.Create);

        When(command => command.ArticleId is null, () =>
        {
            RuleFor(command => command.ContainerId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Для отдельного вопроса необходимо указать контейнер.")
                .Must(containerId => LibraryContainerId.Create(containerId!.Value).IsSuccess)
                .WithMessage("Указан недопустимый идентификатор контейнера.");
        });

        RuleFor(command => command.ContainerId)
            .Null()
            .When(command => command.ArticleId is not null)
            .WithMessage("Для связанного вопроса контейнер определяется выбранной статьёй.");

        RuleFor(command => command.ArticleId)
            .Must(articleId => MaterialId.Create(articleId!.Value).IsSuccess)
            .When(command => command.ArticleId is not null)
            .WithMessage("Указан недопустимый идентификатор статьи.");

        RuleFor(command => command.Title).MustBeValueObject(MaterialTitle.Create);

        RuleFor(command => command.Difficulty)
            .IsInEnum()
            .WithMessage("Указана недопустимая сложность материала.");

        RuleFor(command => command.IconKey)
            .MustBeValueObject(MaterialIcon.Create)
            .When(command => command.IconKey is not null);

        RuleFor(command => command.StudyPoints)
            .InclusiveBetween(MaterialExperienceRewards.MIN_POINTS, MaterialExperienceRewards.MAX_POINTS);

        RuleFor(command => command.ReviewPoints)
            .InclusiveBetween(MaterialExperienceRewards.MIN_POINTS, MaterialExperienceRewards.MAX_POINTS);

        RuleFor(command => command.ReviewPoints)
            .LessThan(command => command.StudyPoints)
            .WithMessage("Награда за повторение должна быть меньше награды за первичное изучение.");

        RuleFor(command => command.Tags)
            .Must(tags => tags is null || tags.Count <= Material.MAX_TAGS)
            .WithMessage($"Материал может содержать не более {Material.MAX_TAGS} тегов.");

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