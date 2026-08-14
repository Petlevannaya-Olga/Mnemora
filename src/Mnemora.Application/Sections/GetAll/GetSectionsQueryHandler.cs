using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.GetAll;

public sealed class GetSectionsQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetSectionsQueryHandler> logger)
    : IQueryHandler<IReadOnlyList<SectionListItemDto>, GetSectionsQuery>
{
    public async Task<Result<IReadOnlyList<SectionListItemDto>, Errors>> Handle(
        GetSectionsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await readDbContext.SectionsRead
                .OrderBy(section => section.CreatedAt)
                .ToListAsync(cancellationToken);

            var sections = entities
                .Select(section => new SectionListItemDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.CreatedAt))
                .ToList();

            return sections;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Получение разделов было отменено");

            return CommonErrors.OperationCancelled(
                "sections.get.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось получить разделы");

            return CommonErrors.Db(
                "sections.get.failed",
                "Не удалось загрузить разделы").ToErrors();
        }
    }
}