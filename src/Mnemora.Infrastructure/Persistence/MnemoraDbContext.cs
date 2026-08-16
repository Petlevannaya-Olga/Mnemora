using Microsoft.EntityFrameworkCore;
using Mnemora.Application.Database;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Infrastructure.Persistence;

public sealed class MnemoraDbContext(DbContextOptions<MnemoraDbContext> options) : DbContext(options), IReadDbContext
{
    public DbSet<Section> Sections => Set<Section>();

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<Material> Materials => Set<Material>();

    public IQueryable<Section> SectionsRead => Set<Section>().AsNoTracking();

    public IQueryable<Topic> TopicsRead => Set<Topic>().AsNoTracking();

    public IQueryable<Material> MaterialsRead => Set<Material>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MnemoraDbContext).Assembly);
    }
}