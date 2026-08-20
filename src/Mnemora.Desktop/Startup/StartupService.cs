using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Onboarding;

namespace Mnemora.Desktop.Startup;

public sealed class StartupService(ISettingsService settingsService, OnboardingState onboardingState, IDatabaseInitializer databaseInitializer, ILocalAppDataCleanupService cleanupService, ILogger<StartupService> logger) : IStartupService
{
    public async Task<StartupResult> InitializeAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await ReportProgressAsync(
            progress,
            new StartupProgress(5, "Загружаем настройки", "Подготавливаем конфигурацию Mnemora"),
            cancellationToken);
        await LoadSettingsAsync(cancellationToken);

        bool wasOnboardingCompleted = onboardingState.IsOnboardingCompleted;
        bool storageIsConfigured = wasOnboardingCompleted && !string.IsNullOrWhiteSpace(onboardingState.StoragePath);
        bool editorIsConfigured = HasEditorConfiguration();

        await ReportProgressAsync(
            progress,
            new StartupProgress(20, "Очищаем временные данные", "Проверяем Temp в LocalAppData"),
            cancellationToken);
        LocalAppDataCleanupReport cleanupReport = await cleanupService.CleanupAsync(cancellationToken);

        if (cleanupReport.SkippedCount > 0)
        {
            logger.LogWarning("При очистке временных данных Mnemora пропущено объектов: {SkippedCount}", cleanupReport.SkippedCount);
        }

        await ReportProgressAsync(
            progress,
            new StartupProgress(
                40,
                "Проверяем хранилище",
                storageIsConfigured
                    ? onboardingState.StoragePath
                    : "Хранилище будет настроено в онбординге"),
            cancellationToken);

        if (storageIsConfigured)
        {
            if (!Directory.Exists(onboardingState.StoragePath))
            {
                return StartupResult.Failure($"Папка хранилища не найдена: {onboardingState.StoragePath}");
            }

            await ReportProgressAsync(
                progress,
                new StartupProgress(65, "Инициализируем базу данных", "Проверяем SQLite и миграции"),
                cancellationToken);
            var databaseResult = await databaseInitializer.InitializeAsync(cancellationToken);

            if (databaseResult.IsFailure)
            {
                return StartupResult.Failure(databaseResult.Error.Message);
            }
        }

        await ReportProgressAsync(
            progress,
            new StartupProgress(90, "Подготавливаем приложение", "Завершаем запуск"),
            cancellationToken);

        await ReportProgressAsync(
            progress,
            new StartupProgress(
                100,
                "Готово",
                cleanupReport.SkippedCount == 0
                    ? "Mnemora готова к работе"
                    : $"Mnemora готова к работе. Не удалось удалить временных объектов: {cleanupReport.SkippedCount}"),
            cancellationToken);
        return StartupResult.Success(wasOnboardingCompleted, storageIsConfigured, editorIsConfigured);
    }

    private static async Task ReportProgressAsync(
        IProgress<StartupProgress> progress,
        StartupProgress state,
        CancellationToken cancellationToken)
    {
        progress.Report(state);

        // Даже быстрый этап должен попасть хотя бы в один кадр WPF.
        // Небольшая уступка UI-потоку предотвращает скачок сразу к финалу.
        await Task.Delay(70, cancellationToken);
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            AppSettings settings = await settingsService.LoadAsync(cancellationToken);
            onboardingState.UserName = string.IsNullOrWhiteSpace(settings.UserName) ? null : settings.UserName.Trim();
            onboardingState.StoragePath = string.IsNullOrWhiteSpace(settings.StoragePath) ? null : settings.StoragePath.Trim();
            onboardingState.MarkdownEditor = settings.MarkdownEditor;
            onboardingState.VisualStudioCodePath = string.IsNullOrWhiteSpace(settings.VisualStudioCodePath) ? null : settings.VisualStudioCodePath.Trim();
            onboardingState.ObsidianVaultPath = string.IsNullOrWhiteSpace(settings.ObsidianVaultPath) ? null : settings.ObsidianVaultPath.Trim();
            onboardingState.IsMarkdownEditorVerified = false;
            onboardingState.IsAiConfigured = settings.IsAiConfigured;
            onboardingState.IsOnboardingCompleted = settings.IsOnboardingCompleted;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(exception, "Не удалось загрузить настройки Mnemora. Будет открыт онбординг.");
            ResetOnboardingState();
        }
    }

    private bool HasEditorConfiguration()
    {
        return onboardingState.MarkdownEditor switch
        {
            MarkdownEditorType.VisualStudioCode => !string.IsNullOrWhiteSpace(onboardingState.VisualStudioCodePath),
            MarkdownEditorType.Obsidian => !string.IsNullOrWhiteSpace(onboardingState.ObsidianVaultPath),
            _ => false,
        };
    }

    private void ResetOnboardingState()
    {
        onboardingState.UserName = null;
        onboardingState.StoragePath = null;
        onboardingState.MarkdownEditor = null;
        onboardingState.VisualStudioCodePath = null;
        onboardingState.ObsidianVaultPath = null;
        onboardingState.IsMarkdownEditorVerified = false;
        onboardingState.IsAiConfigured = false;
        onboardingState.IsOnboardingCompleted = false;
        onboardingState.PendingApiKey = null;
    }
}
