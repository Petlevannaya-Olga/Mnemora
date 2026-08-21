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
            StorageValidationFailureKind.MarkerMissing or
            StorageValidationFailureKind.MarkerCorrupted;

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
            StorageValidationFailureKind.None);

    public static StartupResult Failure(
        string errorMessage,
        StorageValidationFailureKind storageFailureKind =
            StorageValidationFailureKind.Other) =>
        new(
            false,
            false,
            false,
            false,
            errorMessage,
            storageFailureKind);
}
