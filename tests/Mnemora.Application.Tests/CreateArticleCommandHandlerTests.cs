using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.Materials;
using Mnemora.Application.Materials.Articles.Create;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Topics;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class CreateArticleCommandHandlerTests
{
    [Fact]
    public async Task SaveFailure_DeletesPreviouslyCreatedMarkdownContent()
    {
        var topicsRepository = new ExistingTopicRepository();
        var materialsRepository = new RecordingMaterialsRepository();
        var contentStore = new RecordingContentStore();
        var transactionManager = new FailingTransactionManager();
        var handler = new CreateArticleCommandHandler(
            topicsRepository,
            materialsRepository,
            contentStore,
            transactionManager,
            NullLogger<CreateArticleCommandHandler>.Instance);
        var command = new CreateArticleCommand(
            Guid.NewGuid(),
            "Article",
            MaterialDifficulty.Medium,
            IconKey: null,
            StudyPoints: 50,
            ReviewPoints: 20,
            BodyMarkdown: "Content");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Article addedArticle = Assert.IsType<Article>(materialsRepository.AddedMaterial);
        Assert.Equal(addedArticle.Id, contentStore.CreatedMaterialId);
        Assert.Equal(addedArticle.Id, contentStore.DeletedMaterialId);
        Assert.Equal(MaterialType.Article, contentStore.DeletedMaterialType);
    }

    private sealed class ExistingTopicRepository : ITopicsRepository
    {
        public void Add(Topic topic) => throw new NotSupportedException();

        public void Remove(Topic topic) => throw new NotSupportedException();

        public Task<Result<Topic?, Error>> GetByIdAsync(
            TopicId topicId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<bool, Error>> ExistsAsync(
            Expression<Func<Topic, bool>> predicate,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<bool, Error>(true));
    }

    private sealed class RecordingMaterialsRepository : IMaterialsRepository
    {
        public Material? AddedMaterial { get; private set; }

        public void Add(Material material) => AddedMaterial = material;

        public void Remove(Material material) => throw new NotSupportedException();

        public Task<Result<Material?, Error>> GetByIdAsync(
            MaterialId materialId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<Question>, Error>> GetQuestionsByArticleIdAsync(
            MaterialId articleId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingContentStore : IMaterialContentStore
    {
        public MaterialId? CreatedMaterialId { get; private set; }
        public MaterialId? DeletedMaterialId { get; private set; }
        public MaterialType? DeletedMaterialType { get; private set; }

        public Task<UnitResult<Error>> CreateArticleAsync(
            MaterialId materialId,
            ArticleContent content,
            CancellationToken cancellationToken)
        {
            CreatedMaterialId = materialId;
            return Task.FromResult(UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> CreateQuestionAsync(
            MaterialId materialId,
            QuestionContent content,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<ArticleContent, Error>> ReadArticleAsync(
            MaterialId materialId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<QuestionContent, Error>> ReadQuestionAsync(
            MaterialId materialId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public UnitResult<Error> Delete(
            MaterialId materialId,
            MaterialType materialType)
        {
            DeletedMaterialId = materialId;
            DeletedMaterialType = materialType;
            return UnitResult.Success<Error>();
        }
    }

    private sealed class FailingTransactionManager : ITransactionManager
    {
        public Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UnitResult<Error>> SaveChangesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(UnitResult.Failure(
                CommonErrors.Db("test.save.failed", "Save failed")));
    }
}
