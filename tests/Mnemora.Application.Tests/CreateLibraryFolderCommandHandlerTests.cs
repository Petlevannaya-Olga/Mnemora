using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Application.LibraryContainers.Create;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class CreateLibraryFolderCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesFolderUnderParent_AndSavesOnce()
    {
        LibraryContainer parent = CreateRoot();
        var repository = new LibraryContainersRepositoryStub(parent);
        var transactionManager = new TransactionManagerStub();
        var handler = CreateHandler(repository, transactionManager);

        var command = new CreateLibraryFolderCommand(
            parent.Id.Value,
            "CLR",
            FolderColor.Blue,
            FolderIcon.DotNet);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        LibraryContainer folder = Assert.Single(repository.Added);
        Assert.Equal(folder.Id.Value, result.Value);
        Assert.Equal(parent.Id, folder.ParentId);
        Assert.Equal(parent.SectionId, folder.SectionId);
        Assert.Equal(1, folder.Depth);
        Assert.Equal("CLR", folder.Name!.Value);
        Assert.Equal(FolderColor.Blue, folder.Color);
        Assert.Equal(FolderIcon.DotNet, folder.Icon);
        Assert.Equal(1, transactionManager.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_RejectsDuplicateFolderNameAmongSiblings()
    {
        LibraryContainer parent = CreateRoot();
        var repository = new LibraryContainersRepositoryStub(parent)
        {
            ExistsResult = true,
        };
        var transactionManager = new TransactionManagerStub();
        var handler = CreateHandler(repository, transactionManager);

        var result = await handler.Handle(
            new CreateLibraryFolderCommand(
                parent.Id.Value,
                "CLR",
                FolderColor.Teal,
                FolderIcon.Folder),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "library.container.folder.name.already.exists",
            result.Error.First().Code);
        Assert.Empty(repository.Added);
        Assert.Equal(0, transactionManager.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_RejectsFolderBelowThirdLevel()
    {
        LibraryContainer root = CreateRoot();
        LibraryContainer level1 = CreateFolder(root, "Level 1");
        LibraryContainer level2 = CreateFolder(level1, "Level 2");
        LibraryContainer level3 = CreateFolder(level2, "Level 3");
        var repository = new LibraryContainersRepositoryStub(level3);
        var transactionManager = new TransactionManagerStub();
        var handler = CreateHandler(repository, transactionManager);

        var result = await handler.Handle(
            new CreateLibraryFolderCommand(
                level3.Id.Value,
                "Level 4",
                FolderColor.Teal,
                FolderIcon.Folder),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "library.container.maximum.folder.depth.exceeded",
            result.Error.First().Code);
        Assert.Empty(repository.Added);
        Assert.Equal(0, transactionManager.SaveChangesCallCount);
    }

    private static CreateLibraryFolderCommandHandler CreateHandler(
        ILibraryContainersRepository repository,
        ITransactionManager transactionManager) =>
        new(
            repository,
            transactionManager,
            NullLogger<CreateLibraryFolderCommandHandler>.Instance);

    private static LibraryContainer CreateRoot() =>
        LibraryContainer.CreateRoot(SectionId.New()).Value;

    private static LibraryContainer CreateFolder(
        LibraryContainer parent,
        string name) =>
        LibraryContainer.CreateFolder(
            parent,
            FolderName.Create(name).Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

    private sealed class LibraryContainersRepositoryStub(
        LibraryContainer? container)
        : ILibraryContainersRepository
    {
        public List<LibraryContainer> Added { get; } = [];
        public bool ExistsResult { get; set; }

        public void Add(LibraryContainer libraryContainer) => Added.Add(libraryContainer);
        public void Remove(LibraryContainer libraryContainer) => throw new NotSupportedException();

        public Task<Result<LibraryContainer?, Error>> GetByIdAsync(
            LibraryContainerId containerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<LibraryContainer?, Error>(container));

        public Task<Result<LibraryContainer?, Error>> GetRootBySectionIdAsync(
            SectionId sectionId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<LibraryContainer, bool>> predicate,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(ExistsResult));
    }

    private sealed class TransactionManagerStub : ITransactionManager
    {
        public int SaveChangesCallCount { get; private set; }

        public Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UnitResult<Error>> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.FromResult(UnitResult.Success<Error>());
        }
    }
}
