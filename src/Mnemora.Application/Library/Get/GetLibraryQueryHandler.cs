using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Get;

public sealed class GetLibraryQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryQueryHandler> logger)
    : IQueryHandler<IReadOnlyList<LibrarySectionDto>, GetLibraryQuery>
{
    public async Task<Result<IReadOnlyList<LibrarySectionDto>, Errors>> Handle(
        GetLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sections = await readDbContext.SectionsRead
                .OrderBy(section => section.CreatedAt)
                .ToListAsync(cancellationToken);

            var topics = await readDbContext.TopicsRead
                .OrderBy(topic => topic.CreatedAt)
                .ToListAsync(cancellationToken);

            var topicsBySection = topics
                .GroupBy(topic => topic.SectionId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(topic => new LibraryTopicDto(
                            topic.Id.Value,
                            topic.Name.Value,
                            topic.CreatedAt))
                        .ToList());

            var result = sections
                .Select(section =>
                {
                    topicsBySection.TryGetValue(
                        section.Id,
                        out var sectionTopics);

                    return new LibrarySectionDto(
                        section.Id.Value,
                        section.Name.Value,
                        section.CreatedAt,
                        sectionTopics ?? []);
                })
                .ToList();

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                    "library.loading.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось загрузить библиотеку");

            return CommonErrors.Db(
                    "library.loading.failed",
                    "Не удалось загрузить библиотеку")
                .ToErrors();
        }
    }
}