using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Application.Sections;
using Mnemora.Application.Topics;
using Mnemora.Application.Topics.Create;
using Mnemora.Application.Topics.Delete;
using Mnemora.Application.Topics.Update;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class TopicContainerBridgeTests
{
    [Fact]
    public async Task CreateTopic_AddsMatchingFirstLevelFolder_AndSavesOnce()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken ct = cancellationTokenSource.Token;

        SectionId sectionId = SectionId.New();
        LibraryContainer root = LibraryContainer.CreateRoot(sectionId).Value;

        var sectionsRepository = new SectionsRepositoryStub(sectionExists: true);
        var topicsRepository = new TopicsRepositoryStub();
        var containersRepository = new LibraryContainersRepositoryStub(root);
        var transactionManager = new TransactionManagerStub();

        var handler = new CreateTopicCommandHandler(
            sectionsRepository,
            topicsRepository,
            containersRepository,
            transactionManager,
            NullLogger<CreateTopicCommandHandler>.Instance);

        var command = new CreateTopicCommand(
            sectionId.Value,
            "CLR",
            TopicColor.Purple,
            TopicIcon.DotNet);

        var result = await handler.Handle(command, ct);

        Assert.True(result.IsSuccess);

        Topic topic = Assert.Single(topicsRepository.Added);
        LibraryContainer folder = Assert.Single(containersRepository.Added);

        Assert.Equal(topic.Id.Value, result.Value);
        Assert.Equal(topic.Id.Value, folder.Id.Value);
        Assert.Equal(root.Id, folder.ParentId);
        Assert.Equal(sectionId, folder.SectionId);
        Assert.Equal(1, folder.Depth);
        Assert.Equal("CLR", folder.Name!.Value);
        Assert.Equal(FolderColor.Purple, folder.Color);
        Assert.Equal(FolderIcon.DotNet, folder.Icon);

        Assert.Equal(ct, sectionsRepository.ExistsCancellationToken);
        Assert.Equal(ct, topicsRepository.ExistsCancellationToken);
        Assert.Equal(ct, containersRepository.RootCancellationToken);
        Assert.Equal(ct, transactionManager.SaveChangesCancellationToken);
        Assert.Equal(1, transactionManager.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateTopic_UpdatesMatchingFolder_AndSavesOnce()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken ct = cancellationTokenSource.Token;

        SectionId sectionId = SectionId.New();
        LibraryContainer root = LibraryContainer.CreateRoot(sectionId).Value;
        Topic topic = Topic.Create(
            sectionId,
            TopicName.Create("CLR").Value,
            TopicColor.Purple,
            TopicIcon.DotNet);

        LibraryContainer folder = CreateFolderForTopic(root, topic);

        var topicsRepository = new TopicsRepositoryStub(topic);
        var containersRepository = new LibraryContainersRepositoryStub(root, folder);
        var transactionManager = new TransactionManagerStub();

        var handler = new UpdateTopicCommandHandler(
            topicsRepository,
            containersRepository,
            transactionManager,
            NullLogger<UpdateTopicCommandHandler>.Instance);

        var command = new UpdateTopicCommand(
            topic.Id.Value,
            "Runtime",
            TopicColor.Blue,
            TopicIcon.Code);

        var result = await handler.Handle(command, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(topic.Id.Value, result.Value);

        Assert.Equal("Runtime", topic.Name.Value);
        Assert.Equal(TopicColor.Blue, topic.Color);
        Assert.Equal(TopicIcon.Code, topic.Icon);

        Assert.Equal("Runtime", folder.Name!.Value);
        Assert.Equal(FolderColor.Blue, folder.Color);
        Assert.Equal(FolderIcon.Code, folder.Icon);

        Assert.Equal(ct, topicsRepository.GetByIdCancellationToken);
        Assert.Equal(ct, topicsRepository.ExistsCancellationToken);
        Assert.Equal(ct, containersRepository.GetByIdCancellationToken);
        Assert.Equal(ct, transactionManager.SaveChangesCancellationToken);
        Assert.Equal(1, transactionManager.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteTopic_RemovesMatchingFolder_AndSavesOnce()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken ct = cancellationTokenSource.Token;

        SectionId sectionId = SectionId.New();
        LibraryContainer root = LibraryContainer.CreateRoot(sectionId).Value;
        Topic topic = Topic.Create(
            sectionId,
            TopicName.Create("CLR").Value,
            TopicColor.Purple,
            TopicIcon.DotNet);

        LibraryContainer folder = CreateFolderForTopic(root, topic);

        var topicsRepository = new TopicsRepositoryStub(topic);
        var containersRepository = new LibraryContainersRepositoryStub(root, folder);
        var transactionManager = new TransactionManagerStub();

        var handler = new DeleteTopicCommandHandler(
            topicsRepository,
            containersRepository,
            transactionManager,
            NullLogger<DeleteTopicCommandHandler>.Instance);

        var result = await handler.Handle(
            new DeleteTopicCommand(topic.Id.Value),
            ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(topic.Id.Value, result.Value);
        Assert.Same(topic, Assert.Single(topicsRepository.Removed));
        Assert.Same(folder, Assert.Single(containersRepository.Removed));

        Assert.Equal(ct, topicsRepository.GetByIdCancellationToken);
        Assert.Equal(ct, containersRepository.GetByIdCancellationToken);
        Assert.Equal(ct, transactionManager.SaveChangesCancellationToken);
        Assert.Equal(1, transactionManager.SaveChangesCallCount);
    }

    private static LibraryContainer CreateFolderForTopic(
        LibraryContainer root,
        Topic topic) =>
        LibraryContainer.CreateFolderWithId(
            LibraryContainerId.Create(topic.Id.Value).Value,
            root,
            FolderName.Create(topic.Name.Value).Value,
            Enum.Parse<FolderColor>(topic.Color.ToString()),
            Enum.Parse<FolderIcon>(topic.Icon.ToString())).Value;

    private sealed class SectionsRepositoryStub(bool sectionExists)
        : ISectionsRepository
    {
        public CancellationToken ExistsCancellationToken { get; private set; }

        public void Add(Section section) =>
            throw new NotSupportedException();

        public void Remove(Section section) =>
            throw new NotSupportedException();

        public Task<Result<Section?, Error>> GetByIdAsync(
            SectionId sectionId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<Section, bool>> predicate,
            CancellationToken cancellationToken)
        {
            ExistsCancellationToken = cancellationToken;
            return Task.FromResult(
                Result.Success<bool, Error>(sectionExists));
        }
    }

    private sealed class TopicsRepositoryStub(Topic? topic = null)
        : ITopicsRepository
    {
        private readonly Topic? _topic = topic;

        public List<Topic> Added { get; } = [];
        public List<Topic> Removed { get; } = [];

        public CancellationToken GetByIdCancellationToken { get; private set; }
        public CancellationToken ExistsCancellationToken { get; private set; }

        public void Add(Topic topic) => Added.Add(topic);

        public void Remove(Topic topic) => Removed.Add(topic);

        public Task<Result<Topic?, Error>> GetByIdAsync(
            TopicId topicId,
            CancellationToken cancellationToken)
        {
            GetByIdCancellationToken = cancellationToken;
            return Task.FromResult(
                Result.Success<Topic?, Error>(_topic));
        }

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<Topic, bool>> predicate,
            CancellationToken cancellationToken)
        {
            ExistsCancellationToken = cancellationToken;
            return Task.FromResult(
                Result.Success<bool, Error>(false));
        }
    }

    private sealed class LibraryContainersRepositoryStub(
        LibraryContainer root,
        LibraryContainer? folder = null)
        : ILibraryContainersRepository
    {
        private readonly LibraryContainer _root = root;
        private readonly LibraryContainer? _folder = folder;

        public List<LibraryContainer> Added { get; } = [];
        public List<LibraryContainer> Removed { get; } = [];

        public CancellationToken RootCancellationToken { get; private set; }
        public CancellationToken GetByIdCancellationToken { get; private set; }

        public void Add(LibraryContainer container) => Added.Add(container);

        public void Remove(LibraryContainer container) => Removed.Add(container);

        public Task<Result<LibraryContainer?, Error>> GetByIdAsync(
            LibraryContainerId containerId,
            CancellationToken cancellationToken)
        {
            GetByIdCancellationToken = cancellationToken;

            LibraryContainer? result =
                _folder?.Id == containerId
                    ? _folder
                    : null;

            return Task.FromResult(
                Result.Success<LibraryContainer?, Error>(result));
        }

        public Task<Result<LibraryContainer?, Error>> GetRootBySectionIdAsync(
            SectionId sectionId,
            CancellationToken cancellationToken)
        {
            RootCancellationToken = cancellationToken;

            LibraryContainer? result =
                _root.SectionId == sectionId
                    ? _root
                    : null;

            return Task.FromResult(
                Result.Success<LibraryContainer?, Error>(result));
        }

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<LibraryContainer, bool>> predicate,
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
