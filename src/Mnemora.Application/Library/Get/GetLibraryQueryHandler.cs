using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Get;

public sealed class GetLibraryQueryHandler(
    IReadDbContext readDbContext)
    : IQueryHandler<
        IReadOnlyList<LibrarySectionDto>,
        GetLibraryQuery>
{
    public async Task<
        Result<IReadOnlyList<LibrarySectionDto>, Errors>>
        Handle(
            GetLibraryQuery query,
            CancellationToken cancellationToken = default)
    {
        var sections = await readDbContext.SectionsRead
            .OrderBy(section => section.CreatedAt)
            .ToListAsync(cancellationToken);

        if (sections.Count == 0)
        {
            return Result.Success<
                IReadOnlyList<LibrarySectionDto>,
                Errors>(
                Array.Empty<LibrarySectionDto>());
        }

        var topics = await readDbContext.TopicsRead
            .OrderBy(topic => topic.CreatedAt)
            .ToListAsync(cancellationToken);

        var topicsBySection = topics
            .GroupBy(topic =>
                topic.SectionId.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(topic =>
                        new LibraryTopicDto(
                            topic.Id.Value,
                            topic.Name.Value,
                            topic.Color.ToString(),
                            topic.Icon.ToString(),
                            topic.CreatedAt))
                    .ToArray());

        var result = sections
            .Select(section =>
                new LibrarySectionDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.Color.ToString(),
                    section.Icon.ToString(),
                    section.CreatedAt,
                    topicsBySection.GetValueOrDefault(
                        section.Id.Value,
                        [])))
            .ToArray();

        return Result.Success<
            IReadOnlyList<LibrarySectionDto>,
            Errors>(
            result);
    }
}