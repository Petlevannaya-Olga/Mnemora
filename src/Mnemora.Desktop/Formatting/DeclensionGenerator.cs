namespace Mnemora.Desktop.Formatting;

public static class DeclensionGenerator
{
    public static string Generate(
        int number,
        string nominative,
        string genitive,
        string plural)
    {
        var titles = new[] { nominative, genitive, plural };
        var cases = new[] { 2, 0, 1, 1, 1, 2 };

        return titles[
            number % 100 is > 4 and < 20
                ? 2
                : cases[number % 10 < 5 ? number % 10 : 5]];
    }
}