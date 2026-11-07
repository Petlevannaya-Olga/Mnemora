using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mnemora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Keeps enum strings in SQLite stable when C# enum member names are refactored.
/// Null properties are handled by EF Core; only non-null values reach converters.
/// </summary>
public sealed class StableEnumStringConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public StableEnumStringConverter()
        : base(
            value => StableEnumNames.ToStoredName(value),
            stored => StableEnumNames.FromStoredName<TEnum>(stored))
    {
    }
}

internal static class StableEnumNames
{
    public static string ToStoredName<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        EnumNameMap<TEnum>.ToStored.TryGetValue(value, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(value));

    public static TEnum FromStoredName<TEnum>(string stored)
        where TEnum : struct, Enum =>
        EnumNameMap<TEnum>.FromStored.TryGetValue(stored, out var value)
            ? value
            : throw new ArgumentException("Unknown enum value in SQLite.", nameof(stored));

    private static class EnumNameMap<TEnum> where TEnum : struct, Enum
    {
        public static readonly IReadOnlyDictionary<TEnum, string> ToStored;
        public static readonly IReadOnlyDictionary<string, TEnum> FromStored;

        static EnumNameMap()
        {
            var toStored = new Dictionary<TEnum, string>();
            var fromStored = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);

            foreach (var value in Enum.GetValues<TEnum>())
            {
                var symbolName = value.ToString();
                var field = typeof(TEnum).GetField(symbolName);
                var savedName = field?
                    .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                    ?? symbolName;

                toStored.Add(value, savedName);
                fromStored.TryAdd(savedName, value);
                fromStored.TryAdd(symbolName, value);
            }

            ToStored = toStored;
            FromStored = fromStored;
        }
    }
}
