using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.LibraryContainers;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.LibraryContainers;

internal sealed class LibraryContainersRepository(
    MnemoraDbContext dbContext,
    ILogger<LibraryContainersRepository> logger)
    : ILibraryContainersRepository
{
    public void Add(LibraryContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        dbContext.LibraryContainers.Add(container);
    }

    public void Remove(LibraryContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        dbContext.LibraryContainers.Remove(container);
    }

    public async Task<Result<LibraryContainer?, Error>> GetByIdAsync(
        LibraryContainerId containerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(containerId);

        try
        {
            var container = await dbContext.LibraryContainers
                .SingleOrDefaultAsync(
                    container => container.Id == containerId,
                    cancellationToken);

            return Result.Success<LibraryContainer?, Error>(container);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "library.container.get.by.id.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить контейнер библиотеки {ContainerId}",
                containerId.Value);

            return CommonErrors.Db(
                "library.container.get.by.id.failed",
                "Не удалось получить контейнер библиотеки");
        }
    }

    public async Task<Result<LibraryContainer?, Error>> GetRootBySectionIdAsync(
        SectionId sectionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sectionId);

        try
        {
            var root = await dbContext.LibraryContainers
                .SingleOrDefaultAsync(
                    container =>
                        container.SectionId == sectionId &&
                        container.ParentId == null,
                    cancellationToken);

            return Result.Success<LibraryContainer?, Error>(root);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "library.container.get.root.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить корневой контейнер раздела {SectionId}",
                sectionId.Value);

            return CommonErrors.Db(
                "library.container.get.root.failed",
                "Не удалось получить корневой контейнер раздела");
        }
    }

    public async Task<Result<bool, Error>> ExistsAsync(
        Expression<Func<LibraryContainer, bool>> predicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        try
        {
            var exists = await dbContext.LibraryContainers.AnyAsync(
                predicate,
                cancellationToken);

            return exists;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "library.container.exists.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось проверить существование контейнера библиотеки");

            return CommonErrors.Db(
                "library.container.exists.failed",
                "Не удалось проверить существование контейнера библиотеки");
        }
    }
}
