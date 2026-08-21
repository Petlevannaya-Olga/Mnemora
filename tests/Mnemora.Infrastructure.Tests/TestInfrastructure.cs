using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Storage;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Tests;

internal static class TestServiceProviderFactory
{
    public static ServiceProvider Create(
        string storagePath,
        bool validateOnBuild = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStoragePathProvider>(
            new FixedStoragePathProvider(storagePath));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddInfrastructure();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = validateOnBuild,
            ValidateScopes = true,
        });
    }

    private sealed class FixedStoragePathProvider(string storagePath)
        : IStoragePathProvider
    {
        public Result<string, Error> GetStoragePath() =>
            Result.Success<string, Error>(storagePath);
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "Mnemora.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
