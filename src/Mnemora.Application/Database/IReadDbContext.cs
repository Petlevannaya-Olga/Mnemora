using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;

namespace Mnemora.Application.Database;

public interface IReadDbContext
{
    IQueryable<Section> SectionsRead { get; }

    IQueryable<LibraryContainer> LibraryContainersRead { get; }

    IQueryable<Material> MaterialsRead { get; }
}