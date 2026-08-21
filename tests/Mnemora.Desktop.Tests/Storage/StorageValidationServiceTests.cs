using System.IO;
using Mnemora.Desktop.Startup;
using Mnemora.Desktop.Storage;
using Xunit;

namespace Mnemora.Desktop.Tests.Storage;

public sealed class StorageValidationServiceTests
    : IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _localRootPath = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests.Local",
        Guid.NewGuid().ToString("N"));

    private readonly StorageValidationService _service;

    public StorageValidationServiceTests()
    {
        _service = new StorageValidationService(
            new TestPathProvider(
                _localRootPath));
    }

    [Fact]
    public async Task PrepareAsync_ForEmptyDirectory_CreatesValidMarker()
    {
        Directory.CreateDirectory(_storagePath);

        StorageValidationResult prepareResult =
            await _service.PrepareAsync(
                _storagePath);

        StorageValidationResult configuredResult =
            _service.ValidateConfigured(
                _storagePath);

        Assert.True(prepareResult.IsValid);
        Assert.True(configuredResult.IsValid);
        Assert.True(
            File.Exists(
                Path.Combine(
                    _storagePath,
                    ".mnemora")));
    }

    [Fact]
    public void ValidateConfigured_WhenStorageWasDeleted_ReturnsFailure()
    {
        Directory.CreateDirectory(_storagePath);
        Directory.Delete(
            _storagePath,
            recursive: true);

        StorageValidationResult result =
            _service.ValidateConfigured(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Contains(
            "не найдена",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            Directory.Exists(_storagePath));
    }

    [Fact]
    public void ValidateConfigured_WhenMarkerWasDeleted_ReturnsFailure()
    {
        Directory.CreateDirectory(_storagePath);

        StorageValidationResult result =
            _service.ValidateConfigured(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.MarkerMissing,
            result.FailureKind);
        Assert.DoesNotContain(
            ".mnemora",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateConfigured_WhenMarkerIsDamaged_ReturnsFailure()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                ".mnemora"),
            "not-json");

        StorageValidationResult result =
            _service.ValidateConfigured(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.MarkerCorrupted,
            result.FailureKind);

        string errorMessage =
            Assert.IsType<string>(
                result.ErrorMessage);

        Assert.NotEmpty(errorMessage);
        Assert.DoesNotContain(
            ".mnemora",
            errorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateConfigured_WhenMarkerVersionIsUnsupported_ReturnsFailure()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                ".mnemora"),
            "{\"formatVersion\":999}");

        StorageValidationResult result =
            _service.ValidateConfigured(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.StorageVersionIsNewer,
            result.FailureKind);
        Assert.Contains(
            "более новой версии",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCandidate_ForNonEmptyForeignDirectory_ReturnsFailure()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "foreign.txt"),
            "foreign");

        StorageValidationResult result =
            _service.ValidateCandidate(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.MarkerMissing,
            result.FailureKind);
    }

    [Fact]
    public async Task PrepareAsync_ForNonEmptyForeignDirectory_DoesNotCreateMarker()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "foreign.txt"),
            "foreign");

        StorageValidationResult result =
            await _service.PrepareAsync(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.False(
            File.Exists(
                Path.Combine(
                    _storagePath,
                    ".mnemora")));
        Assert.Equal(
            StorageValidationFailureKind.MarkerMissing,
            result.FailureKind);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"formatVersion\":999}")]
    public async Task PrepareAsync_WhenStorageHasNoData_RecreatesInvalidMarker(
        string markerContent)
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        await File.WriteAllTextAsync(
            markerPath,
            markerContent);

        StorageValidationResult result =
            await _service.PrepareAsync(
                _storagePath);

        Assert.True(result.IsValid);
        Assert.Equal(
            "{\"formatVersion\":1}",
            await File.ReadAllTextAsync(
                markerPath));
    }

    [Fact]
    public async Task PrepareAsync_WhenNonEmptyStorageMarkerIsCorrupted_OffersRepairWithoutOverwritingMarker()
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        await File.WriteAllTextAsync(
            markerPath,
            "not-json");
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "material.md"),
            "material");

        StorageValidationResult result =
            await _service.PrepareAsync(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.MarkerCorrupted,
            result.FailureKind);
        Assert.Equal(
            "not-json",
            await File.ReadAllTextAsync(
                markerPath));
    }

    [Fact]
    public async Task RepairAsync_WhenNonEmptyStorageMarkerIsCorrupted_BacksUpAndRecreatesMarker()
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        await File.WriteAllTextAsync(
            markerPath,
            "not-json");
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "material.md"),
            "material");

        StorageValidationResult result =
            await _service.RepairAsync(
                _storagePath);

        Assert.True(result.IsValid);
        Assert.Equal(
            "{\"formatVersion\":1}",
            await File.ReadAllTextAsync(
                markerPath));

        string backupPath = Assert.Single(
            Directory.GetFiles(
                Path.Combine(
                    _localRootPath,
                    "Recovery")));

        Assert.Equal(
            "not-json",
            await File.ReadAllTextAsync(
                backupPath));
    }

    [Fact]
    public async Task RepairAsync_WhenBackupDirectoryCannotBeCreated_DoesNotOverwriteMarkerOrMaterial()
    {
        Directory.CreateDirectory(_storagePath);
        Directory.CreateDirectory(_localRootPath);

        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        string materialPath = Path.Combine(
            _storagePath,
            "material.md");

        string recoveryPath = Path.Combine(
            _localRootPath,
            "Recovery");

        await File.WriteAllTextAsync(
            markerPath,
            "not-json");

        await File.WriteAllTextAsync(
            materialPath,
            "material");

        // Обычный файл блокирует создание каталога с тем же именем.
        await File.WriteAllTextAsync(
            recoveryPath,
            "block");

        StorageValidationResult result =
            await _service.RepairAsync(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.Other,
            result.FailureKind);
        Assert.Contains(
            "подготовить восстановление",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "not-json",
            await File.ReadAllTextAsync(
                markerPath));
        Assert.Equal(
            "material",
            await File.ReadAllTextAsync(
                materialPath));
        Assert.True(
            File.Exists(recoveryPath));
        Assert.False(
            Directory.Exists(recoveryPath));
    }

    [Fact]
    public async Task RepairAsync_WhenNonEmptyStorageVersionIsNewer_DoesNotDowngradeMarker()
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        const string markerContent =
            "{\"formatVersion\":999}";

        await File.WriteAllTextAsync(
            markerPath,
            markerContent);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "material.md"),
            "material");

        StorageValidationResult result =
            await _service.RepairAsync(
                _storagePath);

        Assert.False(result.IsValid);
        Assert.Equal(
            StorageValidationFailureKind.StorageVersionIsNewer,
            result.FailureKind);
        Assert.Equal(
            markerContent,
            await File.ReadAllTextAsync(
                markerPath));
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    _localRootPath,
                    "Recovery")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(
                _storagePath,
                recursive: true);
        }

        if (Directory.Exists(_localRootPath))
        {
            Directory.Delete(
                _localRootPath,
                recursive: true);
        }
    }

    private sealed class TestPathProvider(
        string rootPath)
        : IMnemoraLocalPathProvider
    {
        public string RootPath => rootPath;

        public string TempPath => Path.Combine(
            RootPath,
            "Temp");
    }
}
