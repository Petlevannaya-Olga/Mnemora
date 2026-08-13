namespace Mnemora.Shared;

public enum ErrorType
{
    /// <summary>
    /// Ошибка валидации
    /// </summary>
    VALIDATION,

    /// <summary>
    /// Ничего не найдено
    /// </summary>
    NOTFOUND,

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