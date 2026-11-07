namespace Mnemora.Infrastructure.Database.Errors;

internal static class SqliteExtendedErrorCodes
{
    #pragma warning disable CA1707
    public const int UNIQUE_CONSTRAINT = 2067;
    #pragma warning restore CA1707
    #pragma warning disable CA1707
    public const int FOREIGN_KEY_CONSTRAINT = 787;
    #pragma warning restore CA1707
}