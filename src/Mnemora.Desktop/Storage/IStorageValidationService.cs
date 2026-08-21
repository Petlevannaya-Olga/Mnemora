namespace Mnemora.Desktop.Storage;

public interface IStorageValidationService
{
    StorageValidationResult ValidateCandidate(
        string? storagePath);

    StorageValidationResult ValidateConfigured(
        string? storagePath);

    Task<StorageValidationResult> PrepareAsync(
        string? storagePath,
        CancellationToken cancellationToken = default);
}

public sealed record StorageValidationResult(
    bool IsValid,
    string? NormalizedPath,
    string? ErrorMessage)
{
    public static StorageValidationResult Success(
        string normalizedPath) =>
        new(true, normalizedPath, null);

    public static StorageValidationResult Failure(
        string errorMessage) =>
        new(false, null, errorMessage);
}
