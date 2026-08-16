namespace Mnemora.Shared;

public static class CommonErrors
{
    public static Error IsEmpty(string propertyName) =>
        new(
            $"{propertyName.ToLowerInvariant()}.is.empty",
            $"Поле {propertyName} не может быть пустым.",
            ErrorType.VALIDATION,
            propertyName);

    public static Error IsRequired(string propertyName) =>
        new(
            $"{propertyName.ToLowerInvariant()}.is.required",
            $"Поле {propertyName} обязательно.",
            ErrorType.VALIDATION,
            propertyName);

    public static Error LengthIsWrong(
        string propertyName,
        int minLength,
        int maxLength) =>
        new(
            $"{propertyName.ToLowerInvariant()}.length.is.wrong",
            $"Длина поля {propertyName} должна быть от {minLength} до {maxLength} символов.",
            ErrorType.VALIDATION,
            propertyName);

    public static Error LengthIsTooShort(
        string propertyName,
        int minLength) =>
        new(
            $"{propertyName.ToLowerInvariant()}.length.is.too.short",
            $"Длина поля {propertyName} должна быть не менее {minLength} символов.",
            ErrorType.VALIDATION,
            propertyName);

    public static Error LengthIsTooLarge(
        string propertyName,
        int maxLength) =>
        new(
            $"{propertyName.ToLowerInvariant()}.length.is.too.large",
            $"Длина поля {propertyName} должна быть не более {maxLength} символов.",
            ErrorType.VALIDATION,
            propertyName);

    public static Error MustBePositive(string propertyName) =>
        new(
            $"{propertyName.ToLowerInvariant()}.must.be.positive",
            $"Значение поля {propertyName} должно быть положительным.",
            ErrorType.VALIDATION,
            propertyName);

    public static Error NotFound(
        string? code,
        string message) =>
        new(
            code ?? "record.not.found",
            message,
            ErrorType.NOT_FOUND);

    public static Error Validation(
        string? code,
        string message,
        string? invalidField = null) =>
        new(
            code ?? "value.is.invalid",
            message,
            ErrorType.VALIDATION,
            invalidField);

    public static Error Conflict(
        string? code,
        string message) =>
        new(
            code ?? "value.is.conflict",
            message,
            ErrorType.CONFLICT);

    public static Error Failure(
        string? code,
        string message) =>
        new(
            code ?? "failure",
            message,
            ErrorType.FAILURE);

    public static Error Db(
        string? code,
        string message) =>
        new(
            code ?? "db.exception",
            message,
            ErrorType.DB);

    public static Error OperationCancelled(string? code) =>
        new(
            code ?? "operation.cancelled",
            "Операция была отменена.",
            ErrorType.CANCELED);

    public static Error CollectionIsEmpty(
        string? code,
        string? message) =>
        new(
            code ?? "collection.is.empty",
            message ?? "Коллекция не может быть пустой.",
            ErrorType.VALIDATION);

    public static Error CollectionContainsDuplicates(
        string? code = null) =>
        new(
            code ?? "collection.contains.duplicates",
            "Коллекция содержит дубликаты.",
            ErrorType.VALIDATION);
}