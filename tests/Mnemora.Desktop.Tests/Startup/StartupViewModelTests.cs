using Mnemora.Desktop.Startup;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Startup;
using Xunit;

namespace Mnemora.Desktop.Tests.Startup;

public sealed class StartupViewModelTests
{
    [Fact]
    public async Task RunAsync_OnSuccess_StoresResultAndRaisesSuccess()
    {
        var service = new FakeStartupService(StartupResult.Success(true, true, true));
        var viewModel = new StartupViewModel(service);
        bool succeeded = false;
        viewModel.StartupSucceeded += (_, _) => succeeded = true;

        await viewModel.RunAsync();

        Assert.True(succeeded);
        Assert.NotNull(viewModel.Result);
        Assert.True(viewModel.Result!.IsSuccess);
        Assert.Equal(100, viewModel.Progress);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RunAsync_OnFailure_ShowsErrorAndDoesNotRaiseSuccess()
    {
        var service = new FakeStartupService(StartupResult.Failure("storage failed"));
        var viewModel = new StartupViewModel(service);
        bool succeeded = false;
        viewModel.StartupSucceeded += (_, _) => succeeded = true;

        await viewModel.RunAsync();

        Assert.False(succeeded);
        Assert.True(viewModel.HasError);
        Assert.Equal("storage failed", viewModel.ErrorMessage);
        Assert.True(viewModel.CanRetry);
    }

    [Fact]
    public async Task OpenOnboardingCommand_OnFailure_RaisesOnboardingRequest()
    {
        var service = new FakeStartupService(
            StartupResult.Failure(
                "storage failed"));
        var viewModel = new StartupViewModel(service);
        bool onboardingRequested = false;

        viewModel.OnboardingRequested +=
            (_, _) => onboardingRequested = true;

        await viewModel.RunAsync();
        viewModel.OpenOnboardingCommand.Execute(null);

        Assert.True(onboardingRequested);
    }

    [Fact]
    public async Task RetryCommand_OnRepairableStorageFailure_RepairsStorageAndRaisesSuccess()
    {
        var service = new FakeStartupService(
            StartupResult.Failure(
                "Служебные настройки повреждены.",
                StorageValidationFailureKind.MarkerCorrupted),
            StartupResult.Success(
                true,
                true,
                true));

        var viewModel = new StartupViewModel(service);
        bool succeeded = false;
        viewModel.StartupSucceeded +=
            (_, _) => succeeded = true;

        await viewModel.RunAsync();

        Assert.True(viewModel.CanRepairStorage);
        Assert.Equal(
            "Восстановить",
            viewModel.ErrorActionText);

        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(1, service.RepairCallCount);
        Assert.True(succeeded);
        Assert.True(viewModel.Result!.IsSuccess);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RunAsync_WhenStorageVersionIsNewer_DoesNotOfferRepair()
    {
        var service = new FakeStartupService(
            StartupResult.Failure(
                "Хранилище создано в более новой версии Mnemora.",
                StorageValidationFailureKind.StorageVersionIsNewer));

        var viewModel = new StartupViewModel(service);

        await viewModel.RunAsync();

        Assert.False(viewModel.CanRepairStorage);
        Assert.Equal(
            "Повторить",
            viewModel.ErrorActionText);
        Assert.Contains(
            "Обновите Mnemora",
            viewModel.ErrorHint,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeStartupService(
        StartupResult result,
        StartupResult? repairResult = null)
        : IStartupService
    {
        public int RepairCallCount { get; private set; }

        public Task<StartupResult> InitializeAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken = default)
        {
            progress.Report(new StartupProgress(25, "Шаг 1"));
            progress.Report(new StartupProgress(100, "Готово"));
            return Task.FromResult(result);
        }

        public Task<StartupResult> RepairStorageAsync(
            IProgress<StartupProgress> progress,
            CancellationToken cancellationToken = default)
        {
            RepairCallCount++;
            progress.Report(
                new StartupProgress(
                    100,
                    "Готово"));

            return Task.FromResult(
                repairResult ?? result);
        }
    }
}
