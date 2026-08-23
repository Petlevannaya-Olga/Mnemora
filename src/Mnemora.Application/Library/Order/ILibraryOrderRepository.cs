using CSharpFunctionalExtensions;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;

namespace Mnemora.Application.Library.Order;

public interface ILibraryOrderRepository
{
    Task<Result<IReadOnlyList<Section>, Error>> GetSectionsAsync(
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Topic>, Error>> GetTopicsAsync(
        SectionId sectionId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LibraryContainer>, Error>> GetFirstLevelFoldersAsync(
        SectionId sectionId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Material>, Error>> GetMaterialsAsync(
        TopicId topicId,
        CancellationToken cancellationToken);
}
