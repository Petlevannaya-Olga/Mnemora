using Microsoft.EntityFrameworkCore;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Infrastructure.Persistence;

public sealed class MnemoraDbContext(DbContextOptions<MnemoraDbContext> options) : DbContext(options), IReadDbContext
{
    public DbSet<Section> Sections => Set<Section>();

    public DbSet<LibraryContainer> LibraryContainers => Set<LibraryContainer>();

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<Material> Materials => Set<Material>();

    public IQueryable<Section> SectionsRead => Set<Section>().AsNoTracking();

    public IQueryable<LibraryContainer> LibraryContainersRead => Set<LibraryContainer>().AsNoTracking();

    public IQueryable<Topic> TopicsRead => Set<Topic>().AsNoTracking();

    public IQueryable<Material> MaterialsRead => Set<Material>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var unicodeContainsMethod = typeof(MnemoraDbFunctions).GetMethod(
            nameof(MnemoraDbFunctions.UnicodeContains),
            [typeof(string), typeof(string)])!;

        modelBuilder
            .HasDbFunction(unicodeContainsMethod)
            .HasName(SqliteFunctions.UnicodeContains);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MnemoraDbContext).Assembly);
    }
}