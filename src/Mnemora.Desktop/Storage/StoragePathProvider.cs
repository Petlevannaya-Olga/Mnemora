using CSharpFunctionalExtensions;
using Mnemora.Application.Storage;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Shared;

namespace Mnemora.Desktop.Storage;

public sealed class StoragePathProvider(OnboardingState onboardingState) : IStoragePathProvider
{
    public Result<string, Error> GetStoragePath()
    {
        if (onboardingState.StoragePath is null) return CommonErrors.IsRequired(nameof(OnboardingState.StoragePath));

        var storagePath = onboardingState.StoragePath.Trim();

        if (storagePath.Length == 0) return CommonErrors.IsEmpty(nameof(OnboardingState.StoragePath));

        return storagePath;
    }
}