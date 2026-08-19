using System.Reflection;
using FluentAssertions;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.ViewModels.Library;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class LibraryPagingArchitectureTests
{
    [Fact]
    public void Sections_UseBoundedPagedWindow()
    {
        FieldInfo? field = typeof(LibraryManagementViewModel).GetField(
            "_simpleSectionWindow",
            BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(BoundedPagedWindow<LibrarySectionOverviewDto>));
    }

    [Fact]
    public void Topics_UseBoundedPagedWindow()
    {
        FieldInfo? field = typeof(LibraryManagementViewModel).GetField(
            "_simpleTopicWindow",
            BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(BoundedPagedWindow<LibraryManagementTopicOverviewDto>));
    }

    [Fact]
    public void Materials_KeepBoundedPageCache()
    {
        FieldInfo? field = typeof(LibraryManagementViewModel).GetField(
            "_simpleMaterialPageCache",
            BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(BoundedPageCache<LibraryManagementMaterialOverviewDto>));
    }
}
