using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mnemora.Infrastructure.Persistence;

public sealed class MnemoraDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<MnemoraDbContext>
{
    public MnemoraDbContext CreateDbContext(string[] args)
    {
        string connectionString = DatabasePathProvider.CreateConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<MnemoraDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new MnemoraDbContext(optionsBuilder.Options);
    }
}