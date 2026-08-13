using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mnemora.Shared;

public sealed record Error(
    string Code,
    string Message,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ErrorType>))]
    ErrorType Type,
    string? InvalidField = null)
{
    public Errors ToErrors() => this;

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static Error Deserialize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        try
        {
            return JsonSerializer.Deserialize<Error>(value)
                   ?? throw new FormatException(
                       "Не удалось десериализовать ошибку.");
        }
        catch (JsonException exception)
        {
            throw new FormatException(
                "Некорректный формат сериализованной ошибки.",
                exception);
        }
    }
}