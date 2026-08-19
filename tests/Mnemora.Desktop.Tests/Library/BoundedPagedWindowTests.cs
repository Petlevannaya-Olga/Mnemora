using FluentAssertions;
using Mnemora.Desktop.ViewModels.Library;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class BoundedPagedWindowTests
{
    [Fact]
    public void Constructor_RejectsCacheSmallerThanVisibleWindow()
    {
        Action act = () => _ = new BoundedPagedWindow<int>(30, 7, 6);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForwardScrolling_ThroughFiveThousandPages_RemainsBounded()
    {
        var window = new BoundedPagedWindow<int>(30, 7, 10);
        window.SetTotalCount(5_000 * 30);

        for (int page = 0; page < 5_000; page++)
        {
            int offset = page * 30;
            window.ShowPage(
                offset,
                Enumerable.Range(offset, 30).ToArray(),
                PageWindowInsert.Append);

            window.VisiblePageCount.Should().BeLessThanOrEqualTo(7);
            window.CachedPageCount.Should().BeLessThanOrEqualTo(10);
            window.FlattenVisiblePages().Count.Should().BeLessThanOrEqualTo(210);
        }

        window.WindowStartOffset.Should().Be((5_000 - 7) * 30);
        window.WindowEndOffset.Should().Be(5_000 * 30);
        window.HasNext.Should().BeFalse();
        window.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void BackwardScrolling_TrimsFarEndInsteadOfGrowingVisibleRows()
    {
        var window = new BoundedPagedWindow<int>(30, 7, 10);
        window.SetTotalCount(600);

        for (int page = 0; page < 10; page++)
        {
            int offset = page * 30;
            window.ShowPage(offset, Enumerable.Range(offset, 30).ToArray(), PageWindowInsert.Append);
        }

        window.WindowStartOffset.Should().Be(90);
        window.WindowEndOffset.Should().Be(300);

        window.ShowPage(60, Enumerable.Range(60, 30).ToArray(), PageWindowInsert.Prepend);
        window.ShowPage(30, Enumerable.Range(30, 30).ToArray(), PageWindowInsert.Prepend);
        window.ShowPage(0, Enumerable.Range(0, 30).ToArray(), PageWindowInsert.Prepend);

        window.VisiblePageCount.Should().Be(7);
        window.WindowStartOffset.Should().Be(0);
        window.VisibleOffsets.Should().Equal(0, 30, 60, 90, 120, 150, 180);
        window.CachedPageCount.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void CachedPreviousPage_CanBeShownAgainWithoutIncreasingCachePastLimit()
    {
        var window = new BoundedPagedWindow<int>(30, 7, 10);
        window.SetTotalCount(900);

        for (int page = 0; page < 12; page++)
        {
            int offset = page * 30;
            window.ShowPage(offset, Enumerable.Range(offset, 30).ToArray(), PageWindowInsert.Append);
        }

        int previousOffset = window.PreviousOffset;
        window.TryGetCachedPage(previousOffset, out IReadOnlyList<int> cached).Should().BeTrue();

        window.ShowPage(previousOffset, cached, PageWindowInsert.Prepend);

        window.VisiblePageCount.Should().Be(7);
        window.CachedPageCount.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void PrefetchOffsets_SkipPagesAlreadyCached()
    {
        var window = new BoundedPagedWindow<int>(30, 7, 10);
        window.SetTotalCount(300);
        window.ShowPage(0, Enumerable.Range(0, 30).ToArray(), PageWindowInsert.Append);
        window.StorePage(30, Enumerable.Range(30, 30).ToArray());

        window.GetPrefetchOffsets(2).Should().Equal(60, 90);
    }

    [Fact]
    public void UpdateViewport_TracksLogicalThirtyItemPage()
    {
        var window = new BoundedPagedWindow<int>(30, 7, 10);
        window.SetTotalCount(5_000);

        for (int offset = 180; offset <= 360; offset += 30)
        {
            window.ShowPage(offset, Enumerable.Range(offset, 30).ToArray(), PageWindowInsert.Append);
        }

        window.UpdateViewport(35.2).Should().BeTrue();
        window.CurrentPageOffset.Should().Be(210);
    }

    [Fact]
    public void Reset_ClearsVisiblePagesCacheAndTotals()
    {
        var window = new BoundedPagedWindow<int>(30, 7, 10);
        window.SetTotalCount(1000);
        window.ShowPage(0, [1, 2, 3], PageWindowInsert.Append);
        window.StorePage(30, [4, 5, 6]);

        window.Reset();

        window.TotalCount.Should().Be(0);
        window.VisiblePageCount.Should().Be(0);
        window.CachedPageCount.Should().Be(0);
        window.WindowStartOffset.Should().Be(0);
        window.WindowEndOffset.Should().Be(0);
        window.CurrentPageOffset.Should().Be(0);
    }
}
