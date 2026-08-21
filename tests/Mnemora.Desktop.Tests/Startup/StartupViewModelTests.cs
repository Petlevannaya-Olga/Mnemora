using Mnemora.Desktop.Startup;
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
    public async Task RunAsync_ClampsReportedProgressToValidRange()
    {
        var service = new ReportingStartupService([-25, 40, 150]);
        var viewModel = new StartupViewModel(service);

        await viewModel.RunAsync();

        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("Step 150", viewModel.Title);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RunAsync_OnUnexpectedException_ShowsErrorAndAllowsRetry()
    {
        var viewModel = new StartupViewModel(new ThrowingStartupService());

        await viewModel.RunAsync();

        Assert.True(viewModel.HasError);
        Assert.Equal("unexpected", viewModel.ErrorMessage);
        Assert.True(viewModel.CanRetry);
        Assert.False(viewModel.IsRunning);
    }

    private sealed class FakeStartupService(StartupResult result) : IStartupService
    {
        public Task<StartupResult> InitializeAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken = default)
        {
            progress.Report(new StartupProgress(25, "Шаг 1"));
            progress.Report(new StartupProgress(100, "Готово"));
            return Task.FromResult(result);
        }
    }

    private sealed class ReportingStartupService(IReadOnlyList<int> percentages)
        : IStartupService
    {
        public Task<StartupResult> InitializeAsync(
            IProgress<StartupProgress> progress,
            CancellationToken cancellationToken = default)
        {
            foreach (int percentage in percentages)
            {
                progress.Report(new StartupProgress(percentage, $"Step {percentage}"));
            }

            return Task.FromResult(StartupResult.Success(true, true, true));
        }
    }

    private sealed class ThrowingStartupService : IStartupService
    {
        public Task<StartupResult> InitializeAsync(
            IProgress<StartupProgress> progress,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");
    }
}
