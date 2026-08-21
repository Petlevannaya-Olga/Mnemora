using Mnemora.Application.Library.GetMaterialsPage;
using Mnemora.Application.Library.GetSectionsPage;
using Mnemora.Application.Library.GetTopicsPage;
using Mnemora.Application.Materials.GetDetails;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class LibraryQueryValidationTests
{
    [Fact]
    public void QueriesWithValidators_OptIntoValidationDecorator()
    {
        Assert.IsAssignableFrom<IQueryValidation>(
            new GetLibrarySectionsPageQuery(null, LibrarySectionSort.Name, 0, 30));
        Assert.IsAssignableFrom<IQueryValidation>(
            new GetLibraryTopicsPageQuery(Guid.NewGuid(), null, LibraryTopicSort.Name, 0, 30));
        Assert.IsAssignableFrom<IQueryValidation>(
            new GetLibraryMaterialsPageQuery(
                Guid.NewGuid(),
                null,
                LibraryMaterialFilter.All,
                LibraryMaterialSort.Name,
                0,
                30));
        Assert.IsAssignableFrom<IQueryValidation>(
            new GetMaterialDetailsQuery(Guid.NewGuid()));
    }

    [Fact]
    public void SectionsPageValidator_RejectsInvalidPagingSearchAndSort()
    {
        var query = new GetLibrarySectionsPageQuery(
            new string('S', 151),
            (LibrarySectionSort)999,
            Offset: -1,
            PageSize: 101);

        var result = new GetLibrarySectionsPageQueryValidator().Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(query.Search));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(query.Sort));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(query.Offset));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(query.PageSize));
    }

    [Fact]
    public void TopicsPageValidator_RejectsEmptySectionId()
    {
        var query = new GetLibraryTopicsPageQuery(
            Guid.Empty,
            null,
            LibraryTopicSort.Name,
            0,
            30);

        var result = new GetLibraryTopicsPageQueryValidator().Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "sectionId");
    }

    [Fact]
    public void MaterialsPageValidator_RejectsEmptyTopicIdAndInvalidEnums()
    {
        var query = new GetLibraryMaterialsPageQuery(
            Guid.Empty,
            null,
            (LibraryMaterialFilter)999,
            (LibraryMaterialSort)999,
            0,
            30);

        var result = new GetLibraryMaterialsPageQueryValidator().Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "topicId");
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(query.Filter));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(query.Sort));
    }

    [Fact]
    public void MaterialDetailsValidator_RejectsEmptyId()
    {
        var result = new GetMaterialDetailsQueryValidator()
            .Validate(new GetMaterialDetailsQuery(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "materialId");
    }
}
