namespace Mnemora.Desktop.ViewModels.Library;

public sealed record LibraryTilesPerRowOption(
    string Name,
    int? Value);

public static class LibraryTilesPerRowOptions
{
    public static LibraryTilesPerRowOption Auto { get; } =
        new("Авто", null);

    public static IReadOnlyList<LibraryTilesPerRowOption> All { get; } =
    [
        Auto,
        new("2", 2),
        new("3", 3),
        new("4", 4),
        new("5", 5),
        new("6", 6),
        new("7", 7),
    ];

    public static LibraryTilesPerRowOption Resolve(int? value)
    {
        return All.FirstOrDefault(option => option.Value == value) ?? Auto;
    }
}
