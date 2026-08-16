using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Order;

public sealed class GetLibraryOrderScopesQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryOrderScopesQueryHandler> logger)
    : IQueryHandler<LibraryOrderScopesDto, GetLibraryOrderScopesQuery>
{
    public async Task<Result<LibraryOrderScopesDto, Errors>> Handle(
        GetLibraryOrderScopesQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sections = await readDbContext.SectionsRead
                .OrderBy(section => section.DisplayOrder)
                .ThenBy(section => section.CreatedAt)
                .ThenBy(section => section.Id)
                .ToListAsync(cancellationToken);
            var topics = await readDbContext.TopicsRead
                .OrderBy(topic => topic.DisplayOrder)
                .ThenBy(topic => topic.CreatedAt)
                .ThenBy(topic => topic.Id)
                .ToListAsync(cancellationToken);
            var topicsBySection = topics
                .GroupBy(topic => topic.SectionId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<LibraryOrderTopicScopeDto>)group
                        .Select(topic => new LibraryOrderTopicScopeDto(topic.Id.Value, topic.Name.Value))
                        .ToArray());
            var result = new LibraryOrderScopesDto(
                sections
                    .Select(section => new LibraryOrderSectionScopeDto(
                        section.Id.Value,
                        section.Name.Value,
                        topicsBySection.GetValueOrDefault(section.Id.Value, [])))
                    .ToArray());
            return Result.Success<LibraryOrderScopesDto, Errors>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled("library.order.scopes.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось получить области настройки порядка библиотеки");
            return CommonErrors.Db(
                    "library.order.scopes.failed",
                    "Не удалось загрузить разделы и темы")
                .ToErrors();
        }
    }
}
