using System.IO;
using System.Security;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Storage;
using Mnemora.Domain.Materials;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Materials.Content;

internal sealed class MarkdownMaterialContentStore(
    IStoragePathProvider storagePathProvider,
    ILogger<MarkdownMaterialContentStore> logger)
    : IMaterialContentStore
{
    private const string MaterialsDirectoryName = "materials";
    private const string ArticlesDirectoryName = "articles";
    private const string QuestionsDirectoryName = "questions";

    public Task<UnitResult<Error>> CreateArticleAsync(
        MaterialId materialId,
        ArticleContent content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(materialId);
        ArgumentNullException.ThrowIfNull(content);

        IReadOnlyDictionary<string, string> files = new Dictionary<string, string>
        {
            ["content.md"] = content.BodyMarkdown
        };

        return CreateAsync(materialId, MaterialType.Article, files, cancellationToken);
    }

    public Task<UnitResult<Error>> CreateQuestionAsync(
        MaterialId materialId,
        QuestionContent content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(materialId);
        ArgumentNullException.ThrowIfNull(content);

        IReadOnlyDictionary<string, string> files = new Dictionary<string, string>
        {
            ["prompt.md"] = content.PromptMarkdown,
            ["answer.md"] = content.ReferenceAnswerMarkdown
        };

        return CreateAsync(materialId, MaterialType.Question, files, cancellationToken);
    }

    public async Task<Result<ArticleContent, Error>> ReadArticleAsync(
        MaterialId materialId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(materialId);

        var bodyResult = await ReadFileAsync(
            materialId,
            MaterialType.Article,
            "content.md",
            cancellationToken);

        if (bodyResult.IsFailure)
        {
            return bodyResult.Error;
        }

        return ArticleContent.Create(bodyResult.Value);
    }

    public async Task<Result<QuestionContent, Error>> ReadQuestionAsync(
        MaterialId materialId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(materialId);

        var promptResult = await ReadFileAsync(
            materialId,
            MaterialType.Question,
            "prompt.md",
            cancellationToken);

        if (promptResult.IsFailure)
        {
            return promptResult.Error;
        }

        var answerResult = await ReadFileAsync(
            materialId,
            MaterialType.Question,
            "answer.md",
            cancellationToken);

        if (answerResult.IsFailure)
        {
            return answerResult.Error;
        }

        return QuestionContent.Create(promptResult.Value, answerResult.Value);
    }

    public UnitResult<Error> Delete(MaterialId materialId, MaterialType materialType)
    {
        ArgumentNullException.ThrowIfNull(materialId);

        var directoryResult = GetMaterialDirectoryPath(materialId, materialType);

        if (directoryResult.IsFailure)
        {
            return directoryResult.Error;
        }

        try
        {
            if (!Directory.Exists(directoryResult.Value))
            {
                return UnitResult.Success<Error>();
            }

            Directory.Delete(directoryResult.Value, recursive: true);
            return UnitResult.Success<Error>();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or SecurityException
                or NotSupportedException)
        {
            logger.LogError(
                exception,
                "Не удалось удалить Markdown-файлы материала {MaterialId}",
                materialId.Value);

            return CommonErrors.Failure(
                "material.content.delete.failed",
                "Не удалось удалить Markdown-файлы материала");
        }
    }

    private async Task<UnitResult<Error>> CreateAsync(
        MaterialId materialId,
        MaterialType materialType,
        IReadOnlyDictionary<string, string> files,
        CancellationToken cancellationToken)
    {
        var directoryResult = GetMaterialDirectoryPath(materialId, materialType);

        if (directoryResult.IsFailure)
        {
            return directoryResult.Error;
        }

        string materialDirectory = directoryResult.Value;
        string? parentDirectory = Path.GetDirectoryName(materialDirectory);

        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return CommonErrors.Failure(
                "material.content.parent.path.not.found",
                "Не удалось определить папку содержимого материала");
        }

        string temporaryDirectory = Path.Combine(
            parentDirectory,
            $".{materialId.Value:N}-{Guid.NewGuid():N}.tmp");

        try
        {
            if (Directory.Exists(materialDirectory) || File.Exists(materialDirectory))
            {
                return CommonErrors.Conflict(
                    "material.content.already.exists",
                    "Markdown-файлы материала уже существуют");
            }

            Directory.CreateDirectory(parentDirectory);
            Directory.CreateDirectory(temporaryDirectory);

            foreach (var file in files)
            {
                string filePath = Path.Combine(temporaryDirectory, file.Key);
                await File.WriteAllTextAsync(filePath, file.Value, cancellationToken);
            }

            Directory.Move(temporaryDirectory, materialDirectory);
            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "material.content.create.cancelled");
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or SecurityException
                or NotSupportedException)
        {
            logger.LogError(
                exception,
                "Не удалось создать Markdown-файлы материала {MaterialId}",
                materialId.Value);

            return CommonErrors.Failure(
                "material.content.create.failed",
                "Не удалось создать Markdown-файлы материала");
        }
        finally
        {
            TryDeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    private async Task<Result<string, Error>> ReadFileAsync(
        MaterialId materialId,
        MaterialType materialType,
        string fileName,
        CancellationToken cancellationToken)
    {
        var directoryResult = GetMaterialDirectoryPath(materialId, materialType);

        if (directoryResult.IsFailure)
        {
            return directoryResult.Error;
        }

        string filePath = Path.Combine(directoryResult.Value, fileName);

        try
        {
            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return Result.Success<string, Error>(content);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "material.content.read.cancelled");
        }
        catch (Exception exception) when (
            exception is FileNotFoundException
                or DirectoryNotFoundException)
        {
            logger.LogWarning(
                exception,
                "Markdown-файл {FileName} материала {MaterialId} не найден",
                fileName,
                materialId.Value);

            return CommonErrors.NotFound(
                "material.content.not.found",
                "Markdown-файл материала не найден");
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or SecurityException
                or NotSupportedException)
        {
            logger.LogError(
                exception,
                "Не удалось прочитать Markdown-файл {FileName} материала {MaterialId}",
                fileName,
                materialId.Value);

            return CommonErrors.Failure(
                "material.content.read.failed",
                "Не удалось прочитать Markdown-файл материала");
        }
    }

    private Result<string, Error> GetMaterialDirectoryPath(
        MaterialId materialId,
        MaterialType materialType)
    {
        var storagePathResult = storagePathProvider.GetStoragePath();

        if (storagePathResult.IsFailure)
        {
            return storagePathResult.Error;
        }

        string typeDirectoryName = materialType switch
        {
            MaterialType.Article => ArticlesDirectoryName,
            MaterialType.Question => QuestionsDirectoryName,
            _ => string.Empty
        };

        if (typeDirectoryName.Length == 0)
        {
            return CommonErrors.Validation(
                "material.type.is.invalid",
                "Указан недопустимый тип материала",
                nameof(materialType));
        }

        return Path.Combine(
            storagePathResult.Value,
            MaterialsDirectoryName,
            typeDirectoryName,
            materialId.Value.ToString("N"));
    }

    private void TryDeleteTemporaryDirectory(string temporaryDirectory)
    {
        try
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or SecurityException
                or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Не удалось удалить временную папку {TemporaryDirectory}",
                temporaryDirectory);
        }
    }
}