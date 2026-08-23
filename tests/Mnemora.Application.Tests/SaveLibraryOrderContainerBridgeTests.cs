using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.Library.Order;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class SaveLibraryOrderContainerBridgeTests
{
    [Fact]
    public async Task Handle_TopicOrder_UpdatesMatchingFirstLevelFolders()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken ct = cancellationTokenSource.Token;

        SectionId sectionId = SectionId.New();
        LibraryContainer root = LibraryContainer.CreateRoot(sectionId).Value;

        Topic firstTopic = CreateTopic(sectionId, "CLR");
        Topic secondTopic = CreateTopic(sectionId, "ASP.NET");

        LibraryContainer firstFolder = CreateFolderForTopic(root, firstTopic);
        LibraryContainer secondFolder = CreateFolderForTopic(root, secondTopic);

        var repository = new LibraryOrderRepositoryStub(
            [firstTopic, secondTopic],
            [firstFolder, secondFolder]);
        var transactionManager = new TransactionManagerStub();

        var handler = new SaveLibraryOrderCommandHandler(
            repository,
            transactionManager,
            NullLogger<SaveLibraryOrderCommandHandler>.Instance);

        var command = new SaveLibraryOrderCommand(
            LibraryOrderTarget.Topics,
            sectionId.Value,
            [secondTopic.Id.Value, firstTopic.Id.Value]);

        var result = await handler.Handle(command, ct);

        Assert.True(result.IsSuccess);

        Assert.Equal(1, firstTopic.DisplayOrder);
        Assert.Equal(0, secondTopic.DisplayOrder);
        Assert.Equal(1, firstFolder.DisplayOrder);
        Assert.Equal(0, secondFolder.DisplayOrder);

        Assert.Equal(ct, repository.TopicsCancellationToken);
        Assert.Equal(ct, repository.FoldersCancellationToken);
        Assert.Equal(ct, transactionManager.SaveChangesCancellationToken);
        Assert.Equal(1, transactionManager.SaveChangesCallCount);
    }

    private static Topic CreateTopic(
        SectionId sectionId,
        string name) =>
        Topic.Create(
            sectionId,
            TopicName.Create(name).Value,
            TopicColor.Teal,
            TopicIcon.Bookmark);

    private static LibraryContainer CreateFolderForTopic(
        LibraryContainer root,
        Topic topic) =>
        LibraryContainer.CreateFolderWithId(
            LibraryContainerId.Create(topic.Id.Value).Value,
            root,
            FolderName.Create(topic.Name.Value).Value,
            FolderColor.Teal,
            FolderIcon.Bookmark).Value;

    private sealed class LibraryOrderRepositoryStub(
        IReadOnlyList<Topic> topics,
        IReadOnlyList<LibraryContainer> folders)
        : ILibraryOrderRepository
    {
        public CancellationToken TopicsCancellationToken { get; private set; }
        public CancellationToken FoldersCancellationToken { get; private set; }

        public Task<Result<IReadOnlyList<Section>, Error>> GetSectionsAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<Topic>, Error>> GetTopicsAsync(
            SectionId sectionId,
            CancellationToken cancellationToken)
        {
            TopicsCancellationToken = cancellationToken;
            return Task.FromResult(
                Result.Success<IReadOnlyList<Topic>, Error>(topics));
        }

        public Task<Result<IReadOnlyList<LibraryContainer>, Error>> GetFirstLevelFoldersAsync(
            SectionId sectionId,
            CancellationToken cancellationToken)
        {
            FoldersCancellationToken = cancellationToken;
            return Task.FromResult(
                Result.Success<IReadOnlyList<LibraryContainer>, Error>(folders));
        }

        public Task<Result<IReadOnlyList<Material>, Error>> GetMaterialsAsync(
            TopicId topicId,
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
