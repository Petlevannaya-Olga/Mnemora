using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Application.Sections;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class LibraryContainerPersistenceTests
{
    [Fact]
    public async Task Repository_SavesAndLoadsRootAndFolder()
    {
        CancellationToken ct = CancellationToken.None;

        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);

        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using (MnemoraDbContext dbContext =
                     await factory.CreateDbContextAsync(ct))
        {
            await dbContext.Database.EnsureCreatedAsync(ct);
        }

        var sectionsRepository = scope.ServiceProvider
            .GetRequiredService<ISectionsRepository>();
        var containersRepository = scope.ServiceProvider
            .GetRequiredService<ILibraryContainersRepository>();
        var transactionManager = scope.ServiceProvider
            .GetRequiredService<ITransactionManager>();

        Section section = CreateSection("C#");

        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;

        LibraryContainer folder =
            CreateFolder(root, "CLR");

        sectionsRepository.Add(section);
        containersRepository.Add(root);
        containersRepository.Add(folder);

        var saveResult = await transactionManager.SaveChangesAsync(ct);

        Assert.True(saveResult.IsSuccess);

        var rootResult = await containersRepository.GetRootBySectionIdAsync(
            section.Id,
            ct);

        var folderResult = await containersRepository.GetByIdAsync(
            folder.Id,
            ct);

        Assert.True(rootResult.IsSuccess);
        Assert.NotNull(rootResult.Value);
        Assert.Equal(root.Id, rootResult.Value.Id);
        Assert.True(rootResult.Value.IsRoot);

        Assert.True(folderResult.IsSuccess);
        Assert.NotNull(folderResult.Value);
        Assert.Equal(folder.Id, folderResult.Value.Id);
        Assert.Equal(root.Id, folderResult.Value.ParentId);
        Assert.Equal(section.Id, folderResult.Value.SectionId);
        Assert.Equal("CLR", folderResult.Value.Name!.Value);
    }

    [Fact]
    public async Task Configuration_AllowsOnlyOneRootPerSection()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Section section = CreateSection("Databases");
        LibraryContainer firstRoot =
            LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer secondRoot =
            LibraryContainer.CreateRoot(section.Id).Value;

        dbContext.Sections.Add(section);
        dbContext.LibraryContainers.AddRange(firstRoot, secondRoot);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Configuration_RejectsDuplicateFolderNameAmongSiblingsIgnoringCase()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Section section = CreateSection("Backend");
        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer first = CreateFolder(root, "Architecture");
        LibraryContainer duplicate = CreateFolder(root, "ARCHITECTURE");

        dbContext.Sections.Add(section);
        dbContext.LibraryContainers.AddRange(root, first, duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Configuration_AllowsSameFolderNameUnderDifferentParents()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Section section = CreateSection("C#");
        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer clr = CreateFolder(root, "CLR");
        LibraryContainer aspNet = CreateFolder(root, "ASP.NET");
        LibraryContainer clrMemory = CreateFolder(clr, "Memory");
        LibraryContainer aspNetMemory = CreateFolder(aspNet, "Memory");

        dbContext.Sections.Add(section);
        dbContext.LibraryContainers.AddRange(
            root,
            clr,
            aspNet,
            clrMemory,
            aspNetMemory);

        await dbContext.SaveChangesAsync();

        int memoryFoldersCount = await dbContext.LibraryContainers
            .CountAsync(container =>
                container.Name != null &&
                container.Name == FolderName.Create("Memory").Value);

        Assert.Equal(2, memoryFoldersCount);
    }

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);

    private static LibraryContainer CreateFolder(
        LibraryContainer parent,
        string name) =>
        LibraryContainer.CreateFolder(
            parent,
            FolderName.Create(name).Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;
}