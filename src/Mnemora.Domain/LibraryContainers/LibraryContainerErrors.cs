using Mnemora.Shared;

namespace Mnemora.Domain.LibraryContainers;

public static class LibraryContainerErrors
{
    public static Error MaximumFolderDepthExceeded(int maxDepth) =>
        CommonErrors.Validation(
            "library.container.maximum.folder.depth.exceeded",
            $"Максимальная глубина вложенности папок — {maxDepth} уровня.",
            "parent");

    public static Error RootContainerCannotBeUpdatedAsFolder() =>
        CommonErrors.Conflict(
            "library.container.root.cannot.be.updated.as.folder",
            "Корневой контейнер раздела нельзя изменять как папку.");

    public static Error FolderColorIsInvalid(string propertyName) =>
        CommonErrors.Validation(
            "library.container.folder.color.is.invalid",
            "Указан недопустимый цвет папки.",
            propertyName);

    public static Error FolderIconIsInvalid(string propertyName) =>
        CommonErrors.Validation(
            "library.container.folder.icon.is.invalid",
            "Указана недопустимая иконка папки.",
            propertyName);
}
