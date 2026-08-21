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

    Task<StorageValidationResult> RepairAsync(
        string? storagePath,
        CancellationToken cancellationToken = default);
}

public enum StorageValidationFailureKind
{
    None,
    Other,
    MarkerMissing,
    MarkerCorrupted,
    StorageVersionIsNewer,
    StorageVersionUnsupported,
}

public sealed record StorageValidationResult(
    bool IsValid,
    string? NormalizedPath,
    string? ErrorMessage,
    StorageValidationFailureKind FailureKind)
{
    public static StorageValidationResult Success(
        string normalizedPath) =>
        new(
            true,
            normalizedPath,
            null,
            StorageValidationFailureKind.None);

    public static StorageValidationResult Failure(
        string errorMessage,
        StorageValidationFailureKind failureKind =
            StorageValidationFailureKind.Other) =>
        new(
            false,
            null,
            errorMessage,
            failureKind);
}
