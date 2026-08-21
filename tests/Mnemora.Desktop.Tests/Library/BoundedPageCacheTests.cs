using FluentAssertions;
using Mnemora.Desktop.ViewModels.Library;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class BoundedPageCacheTests
{
    [Fact]
    public void Constructor_RejectsZeroCapacity()
    {
        Action act = () => _ = new BoundedPageCache<int>(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Set_RejectsNegativeOffsetAndNullItems()
    {
        var cache = new BoundedPageCache<int>(3);

        Action negativeOffset = () => cache.Set(-1, [1]);
        Action nullItems = () => cache.Set(0, null!);

        negativeOffset.Should().Throw<ArgumentOutOfRangeException>();
        nullItems.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Set_NeverGrowsPastConfiguredPageLimit()
    {
        var cache = new BoundedPageCache<int>(10);

        for (int page = 0; page < 5_000; page++)
        {
            cache.Set(page * 30, Enumerable.Range(page * 30, 30).ToArray());
        }

        cache.Count.Should().Be(10);
        cache.GetCachedOffsets().Should().HaveCount(10);
        cache.GetCachedOffsets().Should().Equal(
            Enumerable.Range(4_990, 10).Select(page => page * 30));
    }

    [Fact]
    public void LeastRecentlyUsedPage_IsEvicted()
    {
        var cache = new BoundedPageCache<int>(3);
        cache.Set(0, [0]);
        cache.Set(30, [30]);
        cache.Set(60, [60]);

        cache.TryGet(0, out _).Should().BeTrue(); // touch page 0
        cache.Set(90, [90]);

        cache.Contains(0).Should().BeTrue();
        cache.Contains(30).Should().BeFalse();
        cache.Contains(60).Should().BeTrue();
        cache.Contains(90).Should().BeTrue();
    }

    [Fact]
    public void ReplacingExistingPage_DoesNotIncreaseCount()
    {
        var cache = new BoundedPageCache<int>(3);
        cache.Set(0, [1, 2]);
        cache.Set(0, [3, 4]);

        cache.Count.Should().Be(1);
        cache.TryGet(0, out IReadOnlyList<int> items).Should().BeTrue();
        items.Should().Equal(3, 4);
    }

    [Fact]
    public void Clear_DropsAllCachedPages()
    {
        var cache = new BoundedPageCache<int>(10);
        for (int page = 0; page < 10; page++)
        {
            cache.Set(page * 30, [page]);
        }

        cache.Clear();

        cache.Count.Should().Be(0);
        cache.GetCachedOffsets().Should().BeEmpty();
    }

    [Fact]
    public void BackAndForwardScrolling_DoesNotCauseUnboundedGrowth()
    {
        var cache = new BoundedPageCache<int>(10);

        for (int cycle = 0; cycle < 100; cycle++)
        {
            for (int page = 0; page < 200; page++)
            {
                cache.Set(page * 30, [page]);
            }

            for (int page = 199; page >= 0; page--)
            {
                cache.Set(page * 30, [page]);
            }

            cache.Count.Should().BeLessThanOrEqualTo(10);
        }
    }
}
