using Microsoft.EntityFrameworkCore;
using Mnemora.Application.Database;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Infrastructure.Persistence;

public sealed class MnemoraDbContext(DbContextOptions<MnemoraDbContext> options)
    : DbContext(options), IReadDbContext
{
    private static readonly SqliteUnicodeCollationInterceptor CollationInterceptor = new();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<Topic> Topics => Set<Topic>();

    public IQueryable<Section> SectionsRead => Set<Section>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MnemoraDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = DatabasePathProvider.CreateConnectionString();
            optionsBuilder.UseSqlite(connectionString);
        }

        optionsBuilder.AddInterceptors(CollationInterceptor);
    }
}