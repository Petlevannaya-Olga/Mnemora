using FluentAssertions;
using Mnemora.Desktop.ViewModels.Library;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class LibraryRangeTextFormatterTests
{
    [Theory]
    [InlineData(0, 0, 0, false, "Материалы не найдены")]
    [InlineData(0, 30, 5000, false, "Материалы 1–30 из 5 000")]
    [InlineData(480, 30, 5000, false, "Материалы 481–510 из 5 000")]
    [InlineData(4980, 20, 5000, false, "Материалы 4 981–5 000 из 5 000")]
    [InlineData(30, 30, 127, true, "Материалы 31–60 из 127 найденных")]
    public void Format_UsesAgreedWindowNotation(
        int startOffset,
        int visibleCount,
        int totalCount,
        bool isSearch,
        string expected)
    {
        LibraryRangeTextFormatter.Format(startOffset, visibleCount, totalCount, isSearch)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 29.9, 0)]
    [InlineData(0, 30.0, 30)]
    [InlineData(180, 0, 180)]
    [InlineData(180, 35.2, 210)]
    public void GetPageStartOffset_MapsViewportToThirtyItemDatabasePage(
        int windowStart,
        double verticalOffset,
        int expected)
    {
        LibraryRangeTextFormatter.GetPageStartOffset(windowStart, verticalOffset, 30)
            .Should().Be(expected);
    }

    [Fact]
    public void Format_ClampsEndToTotalCount()
    {
        LibraryRangeTextFormatter.Format(120, 30, 127, false)
            .Should().Be("Материалы 121–127 из 127");
    }

    [Theory]
    [InlineData("Разделы", "Разделы не найдены", 0, 30, 5000, false, "Разделы 1–30 из 5 000")]
    [InlineData("Разделы", "Разделы не найдены", 30, 30, 127, true, "Разделы 31–60 из 127 найденных")]
    [InlineData("Темы", "Темы не найдены", 4980, 20, 5000, false, "Темы 4 981–5 000 из 5 000")]
    [InlineData("Темы", "Темы не найдены", 0, 0, 0, true, "Темы не найдены")]
    public void FormatEntity_UsesSameRangeRulesForSectionsAndTopics(
        string entityLabel,
        string emptyText,
        int startOffset,
        int visibleCount,
        int totalCount,
        bool isSearch,
        string expected)
    {
        LibraryRangeTextFormatter.FormatEntity(
                entityLabel,
                emptyText,
                startOffset,
                visibleCount,
                totalCount,
                isSearch)
            .Should().Be(expected);
    }
}
