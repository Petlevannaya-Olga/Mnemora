namespace Mnemora.Shared;

// CA1707 has no exception for enum members; keep the exception local to this enum.
#pragma warning disable CA1707
public enum ErrorType
{
    /// <summary>
    /// Ошибка валидации
    /// </summary>
    VALIDATION,

    /// <summary>
    /// Ничего не найдено
    /// </summary>
    NOT_FOUND,

    /// <summary>
    /// Серверная ошибка
    /// </summary>
    FAILURE,

    /// <summary>
    /// Конфликт
    /// </summary>
    CONFLICT,

    /// <summary>
    /// Ошибка базы данных
    /// </summary>
    DB,

    /// <summary>
    /// Операция была отменена
    /// </summary>
    CANCELED,
}
#pragma warning restore CA1707
