using Microsoft.EntityFrameworkCore;
using Mnemora.Application.Database;
using Mnemora.Domain.Sections;

namespace Mnemora.Infrastructure.Persistence;

public sealed class MnemoraDbContext(DbContextOptions<MnemoraDbContext> options)
    : DbContext(options), IReadDbContext
{
    public DbSet<Section> Sections => Set<Section>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MnemoraDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public IQueryable<Section> SectionsRead => Set<Section>().AsNoTracking();
}