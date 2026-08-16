using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Storage;

namespace Mnemora.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<SqliteUnicodeCollationInterceptor>();

        services.AddDbContextFactory<MnemoraDbContext>((serviceProvider, optionsBuilder) =>
        {
            var connectionString = GetRequiredConnectionString(serviceProvider);

            optionsBuilder.UseSqlite(connectionString);
            optionsBuilder.AddInterceptors(serviceProvider.GetRequiredService<SqliteUnicodeCollationInterceptor>());
        });

        return services;
    }

    private static string GetRequiredConnectionString(IServiceProvider serviceProvider)
    {
        var storagePathResult = serviceProvider.GetRequiredService<IStoragePathProvider>().GetStoragePath();

        if (storagePathResult.IsFailure) throw new PersistenceConfigurationException(storagePathResult.Error);

        var connectionStringResult = DatabasePathProvider.CreateConnectionString(storagePathResult.Value);

        if (connectionStringResult.IsFailure) throw new PersistenceConfigurationException(connectionStringResult.Error);

        return connectionStringResult.Value;
    }
}