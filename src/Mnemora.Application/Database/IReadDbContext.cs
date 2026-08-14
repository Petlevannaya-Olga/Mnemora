using Mnemora.Domain.Sections;

namespace Mnemora.Application.Database;

public interface IReadDbContext
{
    IQueryable<Section> SectionsRead { get; }
}