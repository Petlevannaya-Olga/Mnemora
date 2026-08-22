using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.LibraryContainers;
using Mnemora.Application.Sections;
using Mnemora.Application.Sections.Create;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class CreateSectionCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsSectionAndItsRootContainer_AndSavesOnce()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken ct = cancellationTokenSource.Token;

        var sectionsRepository = new SectionsRepositoryStub();
        var containersRepository = new LibraryContainersRepositoryStub();
        var transactionManager = new TransactionManagerStub();

        var handler = new CreateSectionCommandHandler(
            sectionsRepository,
            containersRepository,
            transactionManager,
            NullLogger<CreateSectionCommandHandler>.Instance);

        var command = new CreateSectionCommand(
            "C#",
            SectionColor.Teal,
            SectionIcon.Folder);

        var result = await handler.Handle(command, ct);

        Assert.True(result.IsSuccess);

        Section section = Assert.Single(sectionsRepository.Added);
        LibraryContainer root = Assert.Single(containersRepository.Added);

        Assert.Equal(section.Id.Value, result.Value);
        Assert.Equal(section.Id, root.SectionId);
        Assert.True(root.IsRoot);
        Assert.Equal(LibraryContainer.RootDepth, root.Depth);
        Assert.Null(root.ParentId);
        Assert.Null(root.Name);

        Assert.Equal(1, transactionManager.SaveChangesCallCount);
        Assert.Equal(ct, sectionsRepository.ExistsCancellationToken);
        Assert.Equal(ct, transactionManager.SaveChangesCancellationToken);
    }

    private sealed class SectionsRepositoryStub : ISectionsRepository
    {
        public List<Section> Added { get; } = [];

        public CancellationToken ExistsCancellationToken { get; private set; }

        public void Add(Section section)
        {
            Added.Add(section);
        }

        public void Remove(Section section)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Section?, Error>> GetByIdAsync(
            SectionId sectionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Result.Success<Section?, Error>(null));
        }

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<Section, bool>> predicate,
            CancellationToken cancellationToken)
        {
            ExistsCancellationToken = cancellationToken;

            return Task.FromResult(
                Result.Success<bool, Error>(false));
        }
    }

    private sealed class LibraryContainersRepositoryStub
        : ILibraryContainersRepository
    {
        public List<LibraryContainer> Added { get; } = [];

        public void Add(LibraryContainer container)
        {
            Added.Add(container);
        }

        public void Remove(LibraryContainer container)
        {
            throw new NotSupportedException();
        }

        public Task<Result<LibraryContainer?, Error>> GetByIdAsync(
            LibraryContainerId containerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Result.Success<LibraryContainer?, Error>(null));
        }

        public Task<Result<LibraryContainer?, Error>> GetRootBySectionIdAsync(
            SectionId sectionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Result.Success<LibraryContainer?, Error>(null));
        }

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<LibraryContainer, bool>> predicate,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Result.Success<bool, Error>(false));
        }
    }

    private sealed class TransactionManagerStub : ITransactionManager
    {
        public int SaveChangesCallCount { get; private set; }

        public CancellationToken SaveChangesCancellationToken { get; private set; }

        public Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

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
