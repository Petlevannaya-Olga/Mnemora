namespace Mnemora.Desktop.Startup;

public interface ILocalAppDataCleanupService
{
    Task<LocalAppDataCleanupReport> CleanupAsync(CancellationToken cancellationToken = default);
}

public sealed record LocalAppDataCleanupReport(int DeletedCount, int SkippedCount);
