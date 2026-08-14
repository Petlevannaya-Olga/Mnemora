using Mnemora.Application.Sections;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;

namespace Mnemora.Infrastructure.Sections;

internal sealed class SectionsRepository(MnemoraDbContext dbContext) : ISectionsRepository
{
    public void Add(Section section)
    {
        dbContext.Sections.Add(section);
    }
}