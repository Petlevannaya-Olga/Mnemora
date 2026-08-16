using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Database;
using Mnemora.Application.Materials;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Sections;
using Mnemora.Application.Topics;
using Mnemora.Infrastructure.Database;
using Mnemora.Infrastructure.Database.Errors;
using Mnemora.Infrastructure.Materials;
using Mnemora.Infrastructure.Materials.Content;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Infrastructure.Sections;
using Mnemora.Infrastructure.Topics;

namespace Mnemora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddPersistence();

        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

        services.AddScoped<IReadDbContext>(provider => provider.GetRequiredService<MnemoraDbContext>());

        services.AddSingleton<ISqliteErrorHandler, TopicSqliteErrorHandler>();
        services.AddSingleton<ISqliteErrorHandler, SectionSqliteErrorHandler>();
        services.AddSingleton<SqliteErrorTranslator>();

        services.AddScoped<ITopicsRepository, TopicsRepository>();
        services.AddScoped<ISectionsRepository, SectionsRepository>();
        services.AddScoped<IMaterialsRepository, MaterialsRepository>();

        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<ITransactionScope, TransactionScope>();

        services.AddSingleton<IMaterialContentStore, MarkdownMaterialContentStore>();
        services.AddTransient<IMaterialContentConsistencyService, MaterialContentConsistencyService>();

        return services;
    }
}