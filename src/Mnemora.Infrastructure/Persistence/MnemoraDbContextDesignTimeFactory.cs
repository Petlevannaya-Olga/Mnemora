using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Persistence;

public sealed class MnemoraDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MnemoraDbContext>
{
    public MnemoraDbContext CreateDbContext(string[] args)
    {
        var contextResult = CreateDbContextResult();

        if (contextResult.IsFailure)
        {
            throw new InvalidOperationException(contextResult.Error.Message);
        }

        return contextResult.Value;
    }

    private static Result<MnemoraDbContext, Error> CreateDbContextResult()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), "Mnemora", "DesignTime");
        Directory.CreateDirectory(storagePath);

        var connectionStringResult = DatabasePathProvider.CreateConnectionString(storagePath);

        if (connectionStringResult.IsFailure) return connectionStringResult.Error;

        var optionsBuilder = new DbContextOptionsBuilder<MnemoraDbContext>();

        optionsBuilder.UseSqlite(connectionStringResult.Value);
        optionsBuilder.AddInterceptors(new SqliteUnicodeCollationInterceptor());

        return new MnemoraDbContext(optionsBuilder.Options);
    }
}
