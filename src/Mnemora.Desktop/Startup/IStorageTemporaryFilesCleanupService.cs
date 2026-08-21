namespace Mnemora.Desktop.Startup;

public interface IStorageTemporaryFilesCleanupService
{
    Task<StorageTemporaryFilesCleanupReport> CleanupAsync(
        string? storagePath,
        CancellationToken cancellationToken = default);
}

public sealed record StorageTemporaryFilesCleanupReport(
    int DeletedCount,
    int SkippedCount);
