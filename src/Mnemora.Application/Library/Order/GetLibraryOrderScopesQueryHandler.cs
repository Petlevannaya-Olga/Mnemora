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
            var folders = await readDbContext.LibraryContainersRead
                .Where(container => container.Depth == 1)
                .OrderBy(container => container.DisplayOrder)
                .ThenBy(container => container.CreatedAt)
                .ThenBy(container => container.Id)
                .ToListAsync(cancellationToken);
            var foldersBySection = folders
                .GroupBy(folder => folder.SectionId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<LibraryOrderFolderScopeDto>)group
                        .Select(folder => new LibraryOrderFolderScopeDto(folder.Id.Value, folder.Name!.Value))
                        .ToArray());
            var result = new LibraryOrderScopesDto(
                sections
                    .Select(section => new LibraryOrderSectionScopeDto(
                        section.Id.Value,
                        section.Name.Value,
                        foldersBySection.GetValueOrDefault(section.Id.Value, [])))
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
                    "Не удалось загрузить разделы и папки")
                .ToErrors();
        }
    }
}
