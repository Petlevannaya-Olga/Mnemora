using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.Sections;
using Mnemora.Application.Sections.Delete;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class DeleteSectionContainerSafetyTests
{
    [Fact]
    public async Task EmptySection_DeletesSuccessfully()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        Section section = NewSection("Empty section");
        db.AddSectionWithRoot(section);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new DeleteSectionCommand(section.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await db.Context.Sections.AnyAsync(s => s.Id == section.Id));
        Assert.False(await db.Context.LibraryContainers.AnyAsync(c => c.SectionId == section.Id));
    }

    [Fact]
    public async Task FolderWithoutLegacyTopic_BlocksSectionDeletion()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        Section section = NewSection("Folder section");
        LibraryContainer root = db.AddSectionWithRoot(section);
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("Folder only").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;
        db.Context.LibraryContainers.Add(folder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Assert.Equal(0, await db.Context.Database.SqlQueryRaw<int>(
            "SELECT count(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'topics'").SingleAsync());
        var handler = CreateHandler(db);
        var result = await handler.Handle(new DeleteSectionCommand(section.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "section.delete.has.folders");
        Assert.True(await db.Context.Sections.AnyAsync(s => s.Id == section.Id));
    }

    [Fact]
    public async Task MaterialInSectionRoot_BlocksSectionDeletion()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        Section section = NewSection("Root material section");
        LibraryContainer root = db.AddSectionWithRoot(section);
        Article article = Article.CreateInContainer(
            root.Id,
            MaterialTitle.Create("Root article").Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        db.Context.Materials.Add(article);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new DeleteSectionCommand(section.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "section.delete.has.materials");
        Assert.True(await db.Context.Sections.AnyAsync(s => s.Id == section.Id));
        Assert.True(await db.Context.Materials.AnyAsync(m => m.Id == article.Id));
    }

    [Fact]
    public async Task OtherSectionsFolders_DoNotPreventDeletingEmptySection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        Section toDelete = NewSection("To delete");
        Section toKeep = NewSection("To keep");
        db.AddSectionWithRoot(toDelete);
        LibraryContainer otherRoot = db.AddSectionWithRoot(toKeep);
        db.Context.LibraryContainers.Add(LibraryContainer.CreateFolder(
            otherRoot,
            FolderName.Create("Other folder").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new DeleteSectionCommand(toDelete.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await db.Context.Sections.AnyAsync(s => s.Id == toDelete.Id));
        Assert.True(await db.Context.Sections.AnyAsync(s => s.Id == toKeep.Id));
        Assert.True(await db.Context.LibraryContainers.AnyAsync(c => c.SectionId == toKeep.Id && c.ParentId != null));
    }

    private static Section NewSection(string name) => Section.Create(
        SectionName.Create(name).Value,
        SectionColor.Teal,
        SectionIcon.Folder);

    private static DeleteSectionCommandHandler CreateHandler(SqliteLibraryTestDatabase db) =>
        new(
            new SectionRepositoryStub(db),
            db.Context,
            new TransactionManagerStub(db),
            NullLogger<DeleteSectionCommandHandler>.Instance);

    private sealed class SectionRepositoryStub(SqliteLibraryTestDatabase db)
        : ISectionsRepository
    {
        public void Add(Section section) => db.Context.Sections.Add(section);
        public void Remove(Section section) => db.Context.Sections.Remove(section);

        public async Task<Result<Section?, Error>> GetByIdAsync(
            SectionId sectionId,
            CancellationToken cancellationToken) =>
            await db.Context.Sections.SingleOrDefaultAsync(
                section => section.Id == sectionId,
                cancellationToken);

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<Section, bool>> predicate,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TransactionManagerStub(SqliteLibraryTestDatabase db)
        : ITransactionManager
    {
        public Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<UnitResult<Error>> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            await db.Context.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
    }
}
