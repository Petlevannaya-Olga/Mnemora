using FluentValidation;
using Mnemora.Domain.Materials;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Materials.Articles.Update;

public sealed class UpdateArticleCommandValidator : AbstractValidator<UpdateArticleCommand>
{
    public UpdateArticleCommandValidator()
    {
        RuleFor(command => command.ArticleId).MustBeValueObject(MaterialId.Create);
        RuleFor(command => command.ContainerId).MustBeValueObject(LibraryContainerId.Create);
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