using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Sections;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Sections;

internal sealed class SectionsRepository(
    MnemoraDbContext dbContext,
    ILogger<SectionsRepository> logger)
    : ISectionsRepository
{
    public void Add(Section section)
    {
        dbContext.Sections.Add(section);
    }

    public void Remove(Section section)
    {
        dbContext.Sections.Remove(section);
    }

    public async Task<Result<Section?, Error>> GetByIdAsync(
        SectionId sectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var section = await dbContext.Sections
                .FirstOrDefaultAsync(
                    section => section.Id == sectionId,
                    cancellationToken);

            return section;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "section.get.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить раздел {SectionId}",
                sectionId.Value);

            return CommonErrors.Db(
                "section.get.failed",
                "Не удалось получить раздел");
        }
    }

    public async Task<Result<bool, Error>> ExistsAsync(
        Expression<Func<Section, bool>> predicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        try
        {
            var exists = await dbContext.Sections
                .AnyAsync(
                    predicate,
                    cancellationToken);

            return exists;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "section.exists.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось проверить существование раздела");

            return CommonErrors.Db(
                "section.exists.failed",
                "Не удалось проверить существование раздела");
        }
    }
}