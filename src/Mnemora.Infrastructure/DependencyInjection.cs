using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Database;
using Mnemora.Application.Sections;
using Mnemora.Infrastructure.Database;
using Mnemora.Infrastructure.Database.Errors;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Infrastructure.Sections;

namespace Mnemora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        string connectionString = DatabasePathProvider.CreateConnectionString();
        services.AddDbContextFactory<MnemoraDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IReadDbContext>(provider => provider.GetRequiredService<MnemoraDbContext>());

        services.AddSingleton<ISqliteErrorHandler, SectionSqliteErrorHandler>();
        services.AddSingleton<SqliteErrorTranslator>();

        services.AddScoped<ISectionsRepository, SectionsRepository>();
        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<ITransactionScope, TransactionScope>();

        return services;
    }
}