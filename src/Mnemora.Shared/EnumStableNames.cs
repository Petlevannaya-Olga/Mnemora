using System.Reflection;
using System.Text.Json.Serialization;

namespace Mnemora.Shared;

/// <summary>
/// Converts enum members to their stable external names without depending on
/// the spelling of the C# enum identifier. JSON and SQLite keep their existing
/// values when enum identifiers are renamed.
/// </summary>
public static class EnumStableNames
{
    public static string ToStableName<TEnum>(this TEnum value)
        where TEnum : struct, Enum =>
        Map<TEnum>.ByValue.TryGetValue(value, out var stableName)
            ? stableName
            : value.ToString();

    public static bool TryParseStableName<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        if (Map<TEnum>.ByName.TryGetValue(text, out value))
        {
            return true;
        }

        // Also accept current enum names and the numeric input previously
        // accepted by Enum.TryParse. Do not normalize stored string values.
        return Enum.TryParse(text, ignoreCase: true, out value);
    }

    public static TEnum ParseStableName<TEnum>(string text)
        where TEnum : struct, Enum =>
        TryParseStableName<TEnum>(text, out var value)
            ? value
            : throw new ArgumentException(
                $"Unknown value '{text}' for {typeof(TEnum).Name}.",
                nameof(text));

    private static class Map<TEnum> where TEnum : struct, Enum
    {
        public static readonly IReadOnlyDictionary<TEnum, string> ByValue;
        public static readonly IReadOnlyDictionary<string, TEnum> ByName;

        static Map()
        {
            var byValue = new Dictionary<TEnum, string>();
            var byName = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);

            foreach (var value in Enum.GetValues<TEnum>())
            {
                var symbolName = value.ToString();
                var field = typeof(TEnum).GetField(symbolName);
                var stableName = field?
                    .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                    ?? symbolName;

                byValue.TryAdd(value, stableName);
                byName.TryAdd(stableName, value);
                byName.TryAdd(symbolName, value);
            }

            ByValue = byValue;
            ByName = byName;
        }
    }
}
