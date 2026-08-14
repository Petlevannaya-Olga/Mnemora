using Mnemora.Domain.Sections;

namespace Mnemora.Application.Sections;

public interface ISectionsRepository
{
    void Add(Section section);
}