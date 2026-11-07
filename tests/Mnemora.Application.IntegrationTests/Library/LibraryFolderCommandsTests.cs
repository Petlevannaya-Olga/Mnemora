using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Application.LibraryContainers.CreateSectionFolder;
using Mnemora.Application.LibraryContainers.Update;
using Mnemora.Application.LibraryContainers.Delete;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class LibraryFolderCommandsTests
{
    [Fact]
    public async Task CreateInSection_WritesOnlyContainer()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (section, root) = await CreateSectionAsync(db);
        var result = await CreateHandler(db).Handle(
            new CreateSectionFolderCommand(section.Id.Value, "CLR", FolderColor.BLUE, FolderIcon.DOT_NET),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        LibraryContainerId createdId = LibraryContainerId.Create(result.Value).Value;
        LibraryContainer folder = await db.Context.LibraryContainers.SingleAsync(x => x.Id == createdId);
        Assert.Equal(root.Id, folder.ParentId);
        Assert.Equal(section.Id, folder.SectionId);
        Assert.Equal(1, folder.Depth);
        Assert.Equal("CLR", folder.Name!.Value);
        Assert.Equal(FolderColor.BLUE, folder.Color);
        Assert.Equal(FolderIcon.DOT_NET, folder.Icon);
    }

    [Fact]
    public async Task CreateInSection_RejectsDuplicateName()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (section, root) = await CreateSectionAsync(db);
        AddFolder(db, root, "Existing");
        await db.Context.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new CreateSectionFolderCommand(section.Id.Value, "Existing", FolderColor.TEAL, FolderIcon.FOLDER),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "library.container.folder.name.already.exists");
        Assert.Equal(1, await db.Context.LibraryContainers.CountAsync(x => x.ParentId == root.Id));
    }

    [Fact]
    public async Task UpdateFolder_ChangesAppearance()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (_, root) = await CreateSectionAsync(db);
        LibraryContainer folder = AddFolder(db, root, "CLR");
        await db.Context.SaveChangesAsync();

        var result = await UpdateHandler(db).Handle(
            new UpdateLibraryFolderCommand(folder.Id.Value, "Runtime", FolderColor.PURPLE, FolderIcon.CODE),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        db.Context.ChangeTracker.Clear();
        LibraryContainer persisted = await db.Context.LibraryContainers.SingleAsync(x => x.Id == folder.Id);
        Assert.Equal("Runtime", persisted.Name!.Value);
        Assert.Equal(FolderColor.PURPLE, persisted.Color);
        Assert.Equal(FolderIcon.CODE, persisted.Icon);
    }

    [Fact]
    public async Task UpdateFolder_RejectsSiblingDuplicate()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (_, root) = await CreateSectionAsync(db);
        LibraryContainer first = AddFolder(db, root, "First");
        AddFolder(db, root, "Second");
        await db.Context.SaveChangesAsync();

        var result = await UpdateHandler(db).Handle(
            new UpdateLibraryFolderCommand(first.Id.Value, "Second", FolderColor.BLUE, FolderIcon.CODE),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "library.container.folder.name.already.exists");
        Assert.Equal("First", first.Name!.Value);
    }

    [Fact]
    public async Task DeleteEmptyFolder_LeavesSectionRoot()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (_, root) = await CreateSectionAsync(db);
        LibraryContainer folder = AddFolder(db, root, "Empty folder");
        await db.Context.SaveChangesAsync();

        var result = await DeleteHandler(db).Handle(
            new DeleteLibraryFolderCommand(folder.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await db.Context.LibraryContainers.AnyAsync(x => x.Id == folder.Id));
        Assert.True(await db.Context.LibraryContainers.AnyAsync(x => x.Id == root.Id));
    }

    [Fact]
    public async Task DeleteFolder_WithMaterial_IsRejectedWithoutDataLoss()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (_, root) = await CreateSectionAsync(db);
        LibraryContainer folder = AddFolder(db, root, "Documents");
        var article = Article.CreateInContainer(
            folder.Id, MaterialTitle.Create("Useful document").Value,
            MaterialDifficulty.MEDIUM, MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        db.Context.Materials.Add(article);
        await db.Context.SaveChangesAsync();

        var result = await DeleteHandler(db).Handle(
            new DeleteLibraryFolderCommand(folder.Id.Value), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "library.folder.delete.has.materials");
        Assert.True(await db.Context.LibraryContainers.AnyAsync(x => x.Id == folder.Id));
        Assert.True(await db.Context.Materials.AnyAsync(x => x.Id == article.Id));
    }

    [Fact]
    public async Task DeleteFolder_WithChildFolder_IsRejected()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (_, root) = await CreateSectionAsync(db);
        LibraryContainer parent = AddFolder(db, root, "Parent");
        LibraryContainer child = AddFolder(db, parent, "Child");
        await db.Context.SaveChangesAsync();

        var result = await DeleteHandler(db).Handle(
            new DeleteLibraryFolderCommand(parent.Id.Value), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "library.folder.delete.has.children");
        Assert.True(await db.Context.LibraryContainers.AnyAsync(x => x.Id == parent.Id));
        Assert.True(await db.Context.LibraryContainers.AnyAsync(x => x.Id == child.Id));
    }

    [Fact]
    public async Task DeleteSectionRoot_IsRejected()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (_, root) = await CreateSectionAsync(db);

        var result = await DeleteHandler(db).Handle(
            new DeleteLibraryFolderCommand(root.Id.Value), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(await db.Context.LibraryContainers.AnyAsync(x => x.Id == root.Id));
    }

    private static Section NewSection() => Section.Create(
        SectionName.Create("Test section").Value, SectionColor.TEAL, SectionIcon.FOLDER);

    private static async Task<(Section Section, LibraryContainer Root)> CreateSectionAsync(
        SqliteLibraryTestDatabase db)
    {
        Section section = NewSection();
        LibraryContainer root = db.AddSectionWithRoot(section);
        await db.Context.SaveChangesAsync();
        return (section, root);
    }

    private static LibraryContainer AddFolder(
        SqliteLibraryTestDatabase db, LibraryContainer parent, string name)
    {
        LibraryContainer folder = LibraryContainer.CreateFolder(
            parent, FolderName.Create(name).Value, FolderColor.TEAL, FolderIcon.FOLDER).Value;
        db.Context.LibraryContainers.Add(folder);
        return folder;
    }

    private static CreateSectionFolderCommandHandler CreateHandler(SqliteLibraryTestDatabase db) =>
        new(new ContainersRepositoryStub(db), new TransactionManagerStub(db),
            NullLogger<CreateSectionFolderCommandHandler>.Instance);

    private static UpdateLibraryFolderCommandHandler UpdateHandler(SqliteLibraryTestDatabase db) =>
        new(new ContainersRepositoryStub(db), new TransactionManagerStub(db),
            NullLogger<UpdateLibraryFolderCommandHandler>.Instance);

    private static DeleteLibraryFolderCommandHandler DeleteHandler(SqliteLibraryTestDatabase db) =>
        new(new ContainersRepositoryStub(db), db.Context, new TransactionManagerStub(db),
            NullLogger<DeleteLibraryFolderCommandHandler>.Instance);

    private sealed class ContainersRepositoryStub(SqliteLibraryTestDatabase db)
        : ILibraryContainersRepository
    {
        public void Add(LibraryContainer folder) => db.Context.LibraryContainers.Add(folder);
        public void Remove(LibraryContainer folder) => db.Context.LibraryContainers.Remove(folder);

        public async Task<Result<LibraryContainer?, Error>> GetByIdAsync(
            LibraryContainerId id, CancellationToken cancellationToken) =>
            await db.Context.LibraryContainers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<Result<LibraryContainer?, Error>> GetRootBySectionIdAsync(
            SectionId id, CancellationToken cancellationToken) =>
            await db.Context.LibraryContainers.SingleOrDefaultAsync(
                x => x.SectionId == id && x.ParentId == null, cancellationToken);

        public async Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<LibraryContainer, bool>> predicate, CancellationToken cancellationToken) =>
            await db.Context.LibraryContainers.AnyAsync(predicate, cancellationToken);
    }

    private sealed class TransactionManagerStub(SqliteLibraryTestDatabase db) : ITransactionManager
    {
        public Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task<UnitResult<Error>> SaveChangesAsync(CancellationToken cancellationToken)
        {
            await db.Context.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
    }
}
