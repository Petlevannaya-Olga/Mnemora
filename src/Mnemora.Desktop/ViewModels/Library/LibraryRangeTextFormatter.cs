using System.Globalization;

namespace Mnemora.Desktop.ViewModels.Library;

public static class LibraryRangeTextFormatter
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(
        int zeroBasedStartOffset,
        int visibleCount,
        int totalCount,
        bool isSearchResult) =>
        FormatEntity(
            "Материалы",
            "Материалы не найдены",
            zeroBasedStartOffset,
            visibleCount,
            totalCount,
            isSearchResult);

    public static string FormatEntity(
        string entityLabel,
        string emptyText,
        int zeroBasedStartOffset,
        int visibleCount,
        int totalCount,
        bool isSearchResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyText);

        if (totalCount <= 0 || visibleCount <= 0)
        {
            return emptyText;
        }

        int normalizedStart = Math.Max(0, zeroBasedStartOffset);
        int start = Math.Min(normalizedStart + 1, totalCount);
        int end = Math.Min(normalizedStart + visibleCount, totalCount);
        string suffix = isSearchResult ? " найденных" : string.Empty;

        return $"{entityLabel} {FormatNumber(start)}–{FormatNumber(end)} из {FormatNumber(totalCount)}{suffix}";
    }

    public static int GetPageStartOffset(
        int windowStartOffset,
        double verticalOffset,
        int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        int normalizedWindowStart = Math.Max(0, windowStartOffset);
        int localFirstVisibleIndex = Math.Max(0, (int)Math.Floor(verticalOffset));
        int globalFirstVisibleIndex = normalizedWindowStart + localFirstVisibleIndex;

        return globalFirstVisibleIndex / pageSize * pageSize;
    }

    private static string FormatNumber(int value) =>
        value.ToString("N0", RussianCulture).Replace('\u00A0', ' ');
}
