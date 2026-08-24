using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionRoot;

public sealed class GetLibrarySectionRootQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibrarySectionRootQueryHandler> logger)
    : IQueryHandler<Guid, GetLibrarySectionRootQuery>
{
    public async Task<Result<Guid, Errors>> Handle(
        GetLibrarySectionRootQuery request,
        CancellationToken cancellationToken = default)
    {
        var sectionIdResult = SectionId.Create(request.SectionId);
        if (sectionIdResult.IsFailure)
            return sectionIdResult.Error.ToErrors();

        try
        {
            LibraryContainer? root = await readDbContext.LibraryContainersRead
                .SingleOrDefaultAsync(
                    container => container.SectionId == sectionIdResult.Value &&
                                 container.ParentId == null,
                    cancellationToken);

            if (root is null)
            {
                return CommonErrors.NotFound(
                    "library.section.root.not.found",
                    $"Корневой контейнер раздела '{request.SectionId}' не найден").ToErrors();
            }

            return Result.Success<Guid, Errors>(root.Id.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "library.section.root.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить root-контейнер раздела {SectionId}",
                request.SectionId);

            return CommonErrors.Db(
                "library.section.root.failed",
                "Не удалось открыть раздел библиотеки").ToErrors();
        }
    }
}
