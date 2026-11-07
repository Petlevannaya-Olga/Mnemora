using System.Text.Json.Serialization;
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

#pragma warning disable CA1707
public enum StorageValidationFailureKind
{
    [JsonStringEnumMemberName("None")]
    NONE,
    [JsonStringEnumMemberName("Other")]
    OTHER,
    [JsonStringEnumMemberName("MarkerMissing")]
    MARKER_MISSING,
    [JsonStringEnumMemberName("MarkerCorrupted")]
    MARKER_CORRUPTED,
    [JsonStringEnumMemberName("StorageVersionIsNewer")]
    STORAGE_VERSION_IS_NEWER,
    [JsonStringEnumMemberName("StorageVersionUnsupported")]
    STORAGE_VERSION_UNSUPPORTED,
}
#pragma warning restore CA1707

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
            StorageValidationFailureKind.NONE);

    public static StorageValidationResult Failure(
        string errorMessage,
        StorageValidationFailureKind failureKind =
            StorageValidationFailureKind.OTHER) =>
        new(
            false,
            null,
            errorMessage,
            failureKind);
}
