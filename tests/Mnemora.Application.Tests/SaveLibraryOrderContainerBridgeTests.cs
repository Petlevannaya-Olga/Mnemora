using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.Library.Order;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class SaveLibraryOrderContainerBridgeTests
{
    [Fact]
    public async Task Handle_FirstLevelFolderOrder_UpdatesFolders()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken ct = cancellationTokenSource.Token;

        SectionId sectionId = SectionId.New();
        LibraryContainer root = LibraryContainer.CreateRoot(sectionId).Value;
        LibraryContainer firstFolder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("CLR").Value,
            FolderColor.TEAL,
            FolderIcon.BOOKMARK).Value;
        LibraryContainer secondFolder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("ASP.NET").Value,
            FolderColor.TEAL,
            FolderIcon.BOOKMARK).Value;

        var repository = new LibraryOrderRepositoryStub([firstFolder, secondFolder]);
        var transactionManager = new TransactionManagerStub();
        var handler = new SaveLibraryOrderCommandHandler(
            repository,
            transactionManager,
            NullLogger<SaveLibraryOrderCommandHandler>.Instance);

        var command = new SaveLibraryOrderCommand(
            LibraryOrderTarget.FOLDERS,
            sectionId.Value,
            [secondFolder.Id.Value, firstFolder.Id.Value]);

        var result = await handler.Handle(command, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, firstFolder.DisplayOrder);
        Assert.Equal(0, secondFolder.DisplayOrder);
        Assert.Equal(ct, repository.FoldersCancellationToken);
        Assert.Equal(ct, transactionManager.SaveChangesCancellationToken);
        Assert.Equal(1, transactionManager.SaveChangesCallCount);
    }

    private sealed class LibraryOrderRepositoryStub(
        IReadOnlyList<LibraryContainer> folders)
        : ILibraryOrderRepository
    {
        public CancellationToken FoldersCancellationToken { get; private set; }

        public Task<Result<IReadOnlyList<Section>, Error>> GetSectionsAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<LibraryContainer>, Error>> GetFirstLevelFoldersAsync(
            SectionId sectionId,
            CancellationToken cancellationToken)
        {
            FoldersCancellationToken = cancellationToken;
            return Task.FromResult(
                Result.Success<IReadOnlyList<LibraryContainer>, Error>(folders));
        }

        public Task<Result<IReadOnlyList<Material>, Error>> GetMaterialsAsync(
            LibraryContainerId containerId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TransactionManagerStub : ITransactionManager
    {
        public int SaveChangesCallCount { get; private set; }
        public CancellationToken SaveChangesCancellationToken { get; private set; }

        public Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UnitResult<Error>> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            SaveChangesCancellationToken = cancellationToken;

            return Task.FromResult(
                UnitResult.Success<Error>());
        }
    }
}
