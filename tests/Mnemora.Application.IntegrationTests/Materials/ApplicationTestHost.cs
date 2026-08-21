using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application;
using Mnemora.Application.Storage;
using Mnemora.Infrastructure;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Application.IntegrationTests.Materials;

internal sealed class ApplicationTestHost : IAsyncDisposable
{
    private ApplicationTestHost(string storagePath, ServiceProvider services)
    {
        StoragePath = storagePath;
        Services = services;
    }

    public string StoragePath { get; }
    public ServiceProvider Services { get; }

    public static async Task<ApplicationTestHost> CreateAsync()
    {
        string storagePath = Path.Combine(
            Path.GetTempPath(),
            "Mnemora.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);

        var services = new ServiceCollection();
        services.AddSingleton<IStoragePathProvider>(
            new FixedStoragePathProvider(storagePath));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        services.AddInfrastructure();

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        try
        {
            var factory = provider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
            await using MnemoraDbContext dbContext = await factory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();
            return new ApplicationTestHost(storagePath, provider);
        }
        catch
        {
            await provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            Directory.Delete(storagePath, recursive: true);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(StoragePath))
        {
            Directory.Delete(StoragePath, recursive: true);
        }
    }

    private sealed class FixedStoragePathProvider(string storagePath)
        : IStoragePathProvider
    {
        public Result<string, Error> GetStoragePath() =>
            Result.Success<string, Error>(storagePath);
    }
}
