using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetSectionRoot;
using Mnemora.Domain.LibraryContainers;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibrarySectionRootQueryHandlerTests
{
    [Fact]
    public async Task ExistingSection_ReturnsActualRootContainerId()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, _) = await db.CreateSectionAndTopicAsync();

        LibraryContainer root = await db.Context.LibraryContainers
            .AsNoTracking()
            .SingleAsync(container => container.SectionId == section.Id && container.ParentId == null);

        var sut = new GetLibrarySectionRootQueryHandler(
            db.Context,
            NullLogger<GetLibrarySectionRootQueryHandler>.Instance);

        var result = await sut.Handle(new GetLibrarySectionRootQuery(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(root.Id.Value);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task MissingSectionRoot_ReturnsNotFound()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var sut = new GetLibrarySectionRootQueryHandler(
            db.Context,
            NullLogger<GetLibrarySectionRootQueryHandler>.Instance);

        var result = await sut.Handle(new GetLibrarySectionRootQuery(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(error => error.Code == "library.section.root.not.found");
    }
}
