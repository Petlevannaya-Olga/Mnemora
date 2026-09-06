using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class LibraryContainerMigrationTests
{
    private const string PreviousMigration =
        "20260819165930_AddLibraryPagingIndexes";

    [Fact]
    public async Task Migration_CreatesRootForEveryExistingSection()
    {
        CancellationToken ct = CancellationToken.None;

        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        IMigrator migrator = dbContext.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(
            PreviousMigration,
            ct);

        Section firstSection = CreateSection("C#");
        Section secondSection = CreateSection("Databases");

        dbContext.Sections.AddRange(
            firstSection,
            secondSection);

        await dbContext.SaveChangesAsync(ct);

        await migrator.MigrateAsync(
            targetMigration: null,
            cancellationToken: ct);

        dbContext.ChangeTracker.Clear();

        List<LibraryContainer> roots = await dbContext.LibraryContainers
            .AsNoTracking()
            .Where(container => container.ParentId == null)
            .ToListAsync(ct);

        Assert.Equal(2, roots.Count);

        Assert.Contains(
            roots,
            root => root.SectionId == firstSection.Id);
        Assert.Contains(
            roots,
            root => root.SectionId == secondSection.Id);

        Assert.All(
            roots,
            root =>
            {
                Assert.True(root.IsRoot);
                Assert.Equal(LibraryContainer.RootDepth, root.Depth);
                Assert.Null(root.ParentId);
                Assert.Null(root.Name);
                Assert.Null(root.Color);
                Assert.Null(root.Icon);
            });
    }

    [Fact]
    public async Task Migration_ConvertsExistingTopicsToFirstLevelFolders()
    {
        CancellationToken ct = CancellationToken.None;

        using var temporaryDirectory = new TemporaryDirectory();

        await using ServiceProvider provider =
            TestServiceProviderFactory.Create(temporaryDirectory.Path);

        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        IMigrator migrator = dbContext.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(
            PreviousMigration,
            ct);

        Section section = CreateSection("C#");

        Topic clr = Topic.Create(
            section.Id,
            TopicName.Create("CLR").Value,
            TopicColor.Purple,
            TopicIcon.DotNet);

        clr.ChangeDisplayOrder(3);

        Topic aspNet = Topic.Create(
            section.Id,
            TopicName.Create("ASP.NET").Value,
            TopicColor.Teal,
            TopicIcon.AspNet);

        aspNet.ChangeDisplayOrder(7);

        dbContext.Sections.Add(section);
        dbContext.Topics.AddRange(clr, aspNet);

        await dbContext.SaveChangesAsync(ct);

        int topicsBeforeMigration = await dbContext.Topics
            .AsNoTracking()
            .CountAsync(ct);

        Assert.Equal(2, topicsBeforeMigration);

        await migrator.MigrateAsync(
            targetMigration: null,
            cancellationToken: ct);

        dbContext.ChangeTracker.Clear();

        List<LibraryContainer> allContainers =
            await dbContext.LibraryContainers
                .AsNoTracking()
                .Where(container => container.SectionId == section.Id)
                .OrderBy(container => container.Depth)
                .ThenBy(container => container.DisplayOrder)
                .ToListAsync(ct);

        Assert.Equal(3, allContainers.Count);

        LibraryContainer root = Assert.Single(
            allContainers,
            container => container.ParentId == null);

        Assert.True(root.IsRoot);
        Assert.Equal(section.Id, root.SectionId);
        Assert.Equal(LibraryContainer.RootDepth, root.Depth);
        Assert.Null(root.Name);
        Assert.Null(root.Color);
        Assert.Null(root.Icon);

        List<LibraryContainer> folders = allContainers
            .Where(container => container.Depth == 1)
            .OrderBy(container => container.DisplayOrder)
            .ToList();

        Assert.Equal(2, folders.Count);

        Assert.All(
            folders,
            folder =>
            {
                Assert.True(folder.IsFolder);
                Assert.Equal(section.Id, folder.SectionId);
                Assert.Equal(root.Id, folder.ParentId);
                Assert.Equal(1, folder.Depth);
            });

        LibraryContainer clrFolder = Assert.Single(
            folders,
            folder => folder.Name?.Value == "CLR");

        Assert.Equal(clr.Id.Value, clrFolder.Id.Value);
        Assert.Equal(FolderColor.Purple, clrFolder.Color);
        Assert.Equal(FolderIcon.DotNet, clrFolder.Icon);
        Assert.Equal(3, clrFolder.DisplayOrder);

        LibraryContainer aspNetFolder = Assert.Single(
            folders,
            folder => folder.Name?.Value == "ASP.NET");

        Assert.Equal(aspNet.Id.Value, aspNetFolder.Id.Value);
        Assert.Equal(FolderColor.Teal, aspNetFolder.Color);
        Assert.Equal(FolderIcon.AspNet, aspNetFolder.Icon);
        Assert.Equal(7, aspNetFolder.DisplayOrder);
    }

    [Fact]
    public async Task Migration_WorksForEmptyDatabase()
    {
        CancellationToken ct = CancellationToken.None;

        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        await dbContext.Database.MigrateAsync(ct);

        int containersCount = await dbContext.LibraryContainers
            .AsNoTracking()
            .CountAsync(ct);

        Assert.Equal(0, containersCount);
    }

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
}