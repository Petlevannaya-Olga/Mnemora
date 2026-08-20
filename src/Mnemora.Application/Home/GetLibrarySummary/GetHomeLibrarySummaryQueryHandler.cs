using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts.Library;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Home.GetLibrarySummary;

public sealed class GetHomeLibrarySummaryQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetHomeLibrarySummaryQueryHandler> logger)
    : IQueryHandler<HomeLibrarySummaryDto, GetHomeLibrarySummaryQuery>
{
    public async Task<Result<HomeLibrarySummaryDto, Errors>> Handle(
        GetHomeLibrarySummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int sectionsCount = await readDbContext.SectionsRead
                .CountAsync(cancellationToken);

            if (sectionsCount == 0)
            {
                return Result.Success<HomeLibrarySummaryDto, Errors>(
                    new HomeLibrarySummaryDto(0, 0, null));
            }

            int topicsCount = await readDbContext.TopicsRead
                .CountAsync(cancellationToken);

            Section? suggestedSection = await readDbContext.SectionsRead
                .Where(section => !readDbContext.TopicsRead
                    .Any(topic => topic.SectionId == section.Id))
                .OrderBy(section => section.CreatedAt)
                .ThenBy(section => section.Id)
                .FirstOrDefaultAsync(cancellationToken);

            suggestedSection ??= await readDbContext.SectionsRead
                .OrderBy(section => section.CreatedAt)
                .ThenBy(section => section.Id)
                .FirstAsync(cancellationToken);

            var suggestedSectionDto = new HomeSuggestedSectionDto(
                suggestedSection.Id.Value,
                suggestedSection.Name.Value);

            return Result.Success<HomeLibrarySummaryDto, Errors>(
                new HomeLibrarySummaryDto(
                    sectionsCount,
                    topicsCount,
                    suggestedSectionDto));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение сводки главной страницы было отменено");

            return CommonErrors.OperationCancelled(
                "home.library-summary.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить сводку библиотеки для главной страницы");

            return CommonErrors.Db(
                "home.library-summary.failed",
                "Не удалось загрузить состояние библиотеки").ToErrors();
        }
    }
}
