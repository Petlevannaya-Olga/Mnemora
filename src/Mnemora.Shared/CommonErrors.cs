namespace Mnemora.Shared;

public static class CommonErrors
{
    public static Error IsEmpty(string propertyName) =>
        new(
            $"{propertyName.ToLowerInvariant()}.is.empty",
            $"Значение не задано для {propertyName}",
            ErrorType.VALIDATION,
            propertyName);

    public static Error IsRequired(string propertyName) =>
        new(
            $"{propertyName.ToLowerInvariant()}.is.required",
            $"Значение не задано для {propertyName}",
            ErrorType.VALIDATION,
            propertyName);

    public static Error LengthIsWrong(string propertyName, int minLength, int maxLength)
        => new(
            $"{propertyName.ToLowerInvariant()}.length.is.wrong",
            $"Значение должно быть длиной от {minLength} до {maxLength} символов для {propertyName}",
            ErrorType.VALIDATION,
            propertyName);

    public static Error LengthIsTooShort(string propertyName, int minLength)
        => new(
            $"{propertyName.ToLowerInvariant()}.length.is.too.short",
            $"Значение должно быть не менее {minLength} символов",
            ErrorType.VALIDATION,
            propertyName);

    public static Error LengthIsTooLarge(string propertyName, int maxLength)
        => new(
            $"{propertyName.ToLowerInvariant()}.length.is.too.large",
            $"Значение должно быть не более {maxLength} символов",
            ErrorType.VALIDATION,
            propertyName);

    public static Error MustBePositive(string propertyName)
        => new(
            $"{propertyName.ToLowerInvariant()}.must.be.positive",
            $"{propertyName} должно быть положительно",
            ErrorType.VALIDATION,
            propertyName);

    public static Error NotFound(string? code, string message, Guid? id = null)
        => new(code ?? "record.not.found", message, ErrorType.NOT_FOUND);

    public static Error Validation(string? code, string message, string? invalidField = null)
        => new(code ?? "value.is.invalid", message, ErrorType.VALIDATION, invalidField);

    public static Error Conflict(string? code, string message)
        => new(code ?? "value.is.conflict", message, ErrorType.CONFLICT);

    public static Error Failure(string? code, string message)
        => new(code ?? "failure", message, ErrorType.FAILURE);

    public static Error Db(string? code, string message)
        => new(code ?? "db.exception", message, ErrorType.DB);

    public static Error OperationCancelled(string? code)
        => new(code ?? "operation.cancelled", "Операция была отменена", ErrorType.CANCELED);

    public static Error CollectionIsEmpty(string? code, string? message)
        => new(code ?? "array.is.empty", message ?? "Массив не может быть пустым", ErrorType.VALIDATION);

    public static Error CollectionContainsDuplicates(string? code = null)
        => new(code ?? "collection.contains.dublicates", "Коллекция содержит дубликаты", ErrorType.VALIDATION);

    public static Error Inactive(Guid id) =>
        new(
            "department.inactive",
            $"Department with '{id}' is inactive.",
            ErrorType.VALIDATION);
}