using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public static class MaterialErrors
{
    public static Error ExperienceRewardIsOutOfRange(string propertyName, int minPoints, int maxPoints) =>
        CommonErrors.Validation(
            "material.experience.reward.is.out.of.range",
            $"Награда должна быть от {minPoints} до {maxPoints} очков включительно.",
            propertyName);

    public static Error ReviewRewardMustBeLessThanStudyReward(string propertyName) =>
        CommonErrors.Validation(
            "material.review.reward.must.be.less.than.study.reward",
            "Награда за повторение должна быть меньше награды за первичное изучение.",
            propertyName);

    public static Error TagsCountIsTooLarge(int maxCount) =>
        CommonErrors.Validation(
            "material.tags.count.is.too.large",
            $"Материал может содержать не более {maxCount} тегов.",
            "tags");

    public static Error TagsContainDuplicates() =>
        CommonErrors.Validation(
            "material.tags.contain.duplicates",
            "Материал не может содержать повторяющиеся теги.",
            "tags");

    public static Error DifficultyIsInvalid(string propertyName) =>
        CommonErrors.Validation(
            "material.difficulty.is.invalid",
            "Указана недопустимая сложность материала.",
            propertyName);

    public static Error IconKeyIsInvalid(string propertyName) =>
        CommonErrors.Validation(
            "material.icon.key.is.invalid",
            "Ключ иконки должен начинаться с латинской буквы и содержать только латинские буквы, цифры и дефисы.",
            propertyName);

    public static Error IconIsNotSupported(string propertyName) =>
        CommonErrors.Validation(
            "material.icon.is.not.supported",
            "Указанная иконка отсутствует во встроенном каталоге Mnemora.",
            propertyName);

    public static Error QuestionAlreadyAttachedToAnotherArticle() =>
        CommonErrors.Conflict(
            "question.already.attached.to.another.article",
            "Вопрос уже связан с другой статьёй. Сначала его нужно открепить.");

    public static Error AttachedQuestionTopicCannotBeChanged() =>
        CommonErrors.Conflict(
            "question.topic.cannot.be.changed.while.attached",
            "Нельзя отдельно изменить тему связанного вопроса. Тема изменяется вместе со статьёй.");

    public static Error QuestionIsNotAttachedToArticle() =>
        CommonErrors.Conflict(
            "question.is.not.attached.to.article",
            "Вопрос не связан с указанной статьёй.");
}