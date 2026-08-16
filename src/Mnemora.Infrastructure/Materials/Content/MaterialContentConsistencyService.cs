using System.Globalization;
using System.Security;
using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Storage;
using Mnemora.Domain.Materials;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Materials.Content;

internal sealed class MaterialContentConsistencyService(
    IStoragePathProvider storagePathProvider,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<MaterialContentConsistencyService> logger)
    : IMaterialContentConsistencyService
{
    public async Task<Result<MaterialContentConsistencyReport, Error>> CheckAndRepairAsync(
        CancellationToken cancellationToken)
    {
        var storagePathResult = storagePathProvider.GetStoragePath();

        if (storagePathResult.IsFailure)
        {
            return storagePathResult.Error;
        }

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var dbContextFactory =
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

            await using var dbContext =
                await dbContextFactory.CreateDbContextAsync(cancellationToken);

            List<MaterialId> articleIds = await dbContext.Materials
                .AsNoTracking()
                .OfType<Article>()
                .Select(article => article.Id)
                .ToListAsync(cancellationToken);

            List<MaterialId> questionIds = await dbContext.Materials
                .AsNoTracking()
                .OfType<Question>()
                .Select(question => question.Id)
                .ToListAsync(cancellationToken);

            var articleResult = CheckMaterialType(
                storagePathResult.Value,
                MaterialType.Article,
                articleIds,
                ["content.md"]);

            var questionResult = CheckMaterialType(
                storagePathResult.Value,
                MaterialType.Question,
                questionIds,
                ["prompt.md", "answer.md"]);

            return new MaterialContentConsistencyReport(
                articleResult.Quarantined + questionResult.Quarantined,
                articleResult.Missing + questionResult.Missing,
                articleResult.Invalid + questionResult.Invalid);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled("material.content.consistency.cancelled");
        }
        catch (PersistenceConfigurationException exception)
        {
            logger.LogError(exception, "Ошибка конфигурации хранилища материалов");
            return exception.Error;
        }
        catch (SqliteException exception)
        {
            logger.LogError(exception, "Не удалось получить материалы для проверки хранилища");

            return CommonErrors.Db(
                "material.content.consistency.database.failed",
                "Не удалось проверить материалы в базе данных");
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or SecurityException
                                             or NotSupportedException)
        {
            logger.LogError(exception, "Не удалось проверить согласованность Markdown-файлов");

            return CommonErrors.Failure(
                "material.content.consistency.failed",
                "Не удалось проверить согласованность Markdown-файлов");
        }
    }

    private ConsistencyResult CheckMaterialType(
        string storagePath,
        MaterialType materialType,
        IReadOnlyCollection<MaterialId> materialIds,
        IReadOnlyCollection<string> requiredFiles)
    {
        string typeDirectory = GetTypeDirectory(storagePath, materialType);
        HashSet<Guid> existingIds = materialIds.Select(id => id.Value).ToHashSet();

        int quarantined = 0;
        int missing = 0;
        int invalid = 0;

        foreach (MaterialId materialId in materialIds)
        {
            string materialDirectory = Path.Combine(typeDirectory, materialId.Value.ToString("N"));

            bool contentIsMissing =
                !Directory.Exists(materialDirectory) ||
                requiredFiles.Any(file => !File.Exists(Path.Combine(materialDirectory, file)));

            if (!contentIsMissing)
            {
                continue;
            }

            missing++;

            logger.LogWarning(
                "Не найдены Markdown-файлы материала {MaterialId} типа {MaterialType}",
                materialId.Value,
                materialType);
        }

        if (!Directory.Exists(typeDirectory))
        {
            return new ConsistencyResult(quarantined, missing, invalid);
        }

        foreach (string directory in Directory.EnumerateDirectories(typeDirectory))
        {
            string directoryName = Path.GetFileName(directory) ?? string.Empty;

            if (Guid.TryParseExact(directoryName, "N", out Guid materialId))
            {
                if (!existingIds.Contains(materialId))
                {
                    MoveToRecovery(storagePath, materialType, directory);
                    quarantined++;
                }

                continue;
            }

            if (directoryName.StartsWith(".", StringComparison.Ordinal))
            {
                MoveToRecovery(storagePath, materialType, directory);
                quarantined++;
                continue;
            }

            invalid++;

            logger.LogWarning(
                "Обнаружена неизвестная папка в хранилище материалов: {Directory}",
                directory);
        }

        return new ConsistencyResult(quarantined, missing, invalid);
    }

    private void MoveToRecovery(
        string storagePath,
        MaterialType materialType,
        string sourceDirectory)
    {
        string recoveryDirectory = Path.Combine(
            storagePath,
            ".mnemora-data",
            "recovery",
            "material-content");

        Directory.CreateDirectory(recoveryDirectory);

        string sourceName = Path.GetFileName(sourceDirectory) ?? "unknown";
        string timestamp = timeProvider.GetUtcNow()
            .ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);

        string destination = Path.Combine(
            recoveryDirectory,
            $"{timestamp}-{materialType.ToString().ToLowerInvariant()}-{sourceName}-{Guid.NewGuid():N}");

        Directory.Move(sourceDirectory, destination);

        logger.LogWarning(
            "Несвязанные Markdown-файлы перемещены в папку восстановления: {Destination}",
            destination);
    }

    private static string GetTypeDirectory(string storagePath, MaterialType materialType)
    {
        string typeDirectoryName = materialType switch
        {
            MaterialType.Article => "articles",
            MaterialType.Question => "questions",
            _ => throw new ArgumentOutOfRangeException(nameof(materialType))
        };

        return Path.Combine(storagePath, "materials", typeDirectoryName);
    }

    private sealed record ConsistencyResult(int Quarantined, int Missing, int Invalid);
}