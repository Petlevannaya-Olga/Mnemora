using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Application.Database;

public interface IReadDbContext
{
    IQueryable<Section> SectionsRead { get; }

    IQueryable<Topic> TopicsRead { get; }
}