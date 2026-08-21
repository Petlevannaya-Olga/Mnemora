using System.IO;
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

    private readonly StorageValidationService _service =
        new();

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
        Assert.Contains(
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
        Assert.Contains(
            "повреждён",
            result.ErrorMessage,
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
        Assert.Contains(
            "неподдерживаемую версию",
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
        Assert.Contains(
            ".mnemora",
            result.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(
                _storagePath,
                recursive: true);
        }
    }
}
