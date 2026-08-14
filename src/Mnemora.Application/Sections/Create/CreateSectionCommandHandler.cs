using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Sections.Create;

public sealed class CreateSectionCommandHandler(
    ISectionsRepository sectionsRepository,
    ITransactionManager transactionManager,
    ILogger<CreateSectionCommandHandler> logger)
    : ICommandHandler<Guid, CreateSectionCommand>
{
    public async Task<Result<Guid, Errors>> Handle(
        CreateSectionCommand command,
        CancellationToken cancellationToken)
    {
        var nameResult = SectionName.Create(command.Name);

        if (nameResult.IsFailure)
        {
            return nameResult.Error.ToErrors();
        }

        var section = Section.Create(nameResult.Value);

        sectionsRepository.Add(section);

        var saveResult =
            await transactionManager.SaveChangesAsync(cancellationToken);

        if (saveResult.IsFailure)
        {
            logger.LogWarning(
                "Не удалось создать раздел {SectionId}. Код ошибки: {ErrorCode}",
                section.Id.Value,
                saveResult.Error.Code);

            return saveResult.Error.ToErrors();
        }

        logger.LogInformation(
            "Создан раздел {SectionId} с названием {SectionName}",
            section.Id.Value,
            section.Name.Value);

        return section.Id.Value;
    }
}