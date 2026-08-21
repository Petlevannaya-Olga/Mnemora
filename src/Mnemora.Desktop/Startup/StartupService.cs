using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Onboarding;

namespace Mnemora.Desktop.Startup;

public sealed class StartupService(
    ISettingsService settingsService,
    OnboardingState onboardingState,
    IDatabaseInitializer databaseInitializer,
    ILocalAppDataCleanupService localCleanupService,
    IStorageTemporaryFilesCleanupService storageCleanupService,
    IStorageValidationService storageValidationService,
    IMarkdownEditorService markdownEditorService,
    ILogger<StartupService> logger)
    : IStartupService
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
        bool storageIsConfigured = false;
        bool editorIsConfigured = false;

        await ReportProgressAsync(
            progress,
            new StartupProgress(20, "Очищаем временные данные", "Проверяем временные каталоги Mnemora"),
            cancellationToken);
        LocalAppDataCleanupReport localCleanupReport =
            await localCleanupService.CleanupAsync(
                cancellationToken);

        int skippedTemporaryFiles =
            localCleanupReport.SkippedCount;

        await ReportProgressAsync(
            progress,
            new StartupProgress(
                40,
                "Проверяем хранилище",
                wasOnboardingCompleted
                    ? onboardingState.StoragePath ??
                      "Путь к хранилищу не указан"
                    : "Хранилище будет настроено в онбординге"),
            cancellationToken);

        if (wasOnboardingCompleted)
        {
            StorageValidationResult storageValidationResult =
                await storageValidationService.PrepareAsync(
                    onboardingState.StoragePath,
                    cancellationToken);

            if (!storageValidationResult.IsValid)
            {
                return StartupResult.Failure(
                    storageValidationResult.ErrorMessage ??
                    "Хранилище Mnemora недоступно.",
                    storageValidationResult.FailureKind);
            }

            onboardingState.StoragePath =
                storageValidationResult.NormalizedPath;

            storageIsConfigured = true;

            MarkdownEditorConfigurationValidationResult
                editorValidationResult =
                    markdownEditorService.ValidateConfiguration(
                        onboardingState.MarkdownEditor,
                        onboardingState.VisualStudioCodePath,
                        onboardingState.ObsidianVaultPath);

            editorIsConfigured =
                editorValidationResult.IsValid;

            StorageTemporaryFilesCleanupReport storageCleanupReport =
                await storageCleanupService.CleanupAsync(
                    onboardingState.StoragePath,
                    cancellationToken);

            skippedTemporaryFiles +=
                storageCleanupReport.SkippedCount;

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

        if (skippedTemporaryFiles > 0)
        {
            logger.LogWarning(
                "При очистке временных данных Mnemora пропущено объектов: {SkippedCount}",
                skippedTemporaryFiles);
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
                skippedTemporaryFiles == 0
                    ? "Mnemora готова к работе"
                    : $"Mnemora готова к работе. Не удалось удалить временных объектов: {skippedTemporaryFiles}"),
            cancellationToken);
        return StartupResult.Success(wasOnboardingCompleted, storageIsConfigured, editorIsConfigured);
    }

    public async Task<StartupResult> RepairStorageAsync(
        IProgress<StartupProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await ReportProgressAsync(
            progress,
            new StartupProgress(
                10,
                "Восстанавливаем хранилище",
                "Проверяем служебные настройки"),
            cancellationToken);

        StorageValidationResult repairResult =
            await storageValidationService.RepairAsync(
                onboardingState.StoragePath,
                cancellationToken);

        if (!repairResult.IsValid)
        {
            return StartupResult.Failure(
                repairResult.ErrorMessage ??
                "Не удалось восстановить хранилище Mnemora.",
                repairResult.FailureKind);
        }

        onboardingState.StoragePath =
            repairResult.NormalizedPath;

        return await InitializeAsync(
            progress,
            cancellationToken);
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
