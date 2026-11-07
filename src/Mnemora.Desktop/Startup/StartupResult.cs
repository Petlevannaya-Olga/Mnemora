using Mnemora.Desktop.Storage;

namespace Mnemora.Desktop.Startup;

public sealed record StartupResult(
    bool IsSuccess,
    bool WasOnboardingCompleted,
    bool StorageIsConfigured,
    bool EditorIsConfigured,
    string? ErrorMessage,
    StorageValidationFailureKind StorageFailureKind)
{
    public bool CanRepairStorage =>
        StorageFailureKind is
            StorageValidationFailureKind.MARKER_MISSING or
            StorageValidationFailureKind.MARKER_CORRUPTED;

    public static StartupResult Success(
        bool wasOnboardingCompleted,
        bool storageIsConfigured,
        bool editorIsConfigured) =>
        new(
            true,
            wasOnboardingCompleted,
            storageIsConfigured,
            editorIsConfigured,
            null,
            StorageValidationFailureKind.NONE);

    public static StartupResult Failure(
        string errorMessage,
        StorageValidationFailureKind storageFailureKind =
            StorageValidationFailureKind.OTHER) =>
        new(
            false,
            false,
            false,
            false,
            errorMessage,
            storageFailureKind);
}
