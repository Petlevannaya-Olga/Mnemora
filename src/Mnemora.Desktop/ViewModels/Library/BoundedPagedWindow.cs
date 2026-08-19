namespace Mnemora.Desktop.ViewModels.Library;

public enum PageWindowInsert
{
    Append,
    Prepend,
}

/// <summary>
/// Maintains a bounded set of visible database pages plus a bounded LRU cache.
/// It contains no UI or database logic and is safe to unit-test independently.
/// </summary>
public sealed class BoundedPagedWindow<T>
{
    private readonly BoundedPageCache<T> _cache;
    private readonly LinkedList<int> _visibleOffsets = [];

    public BoundedPagedWindow(
        int pageSize,
        int visiblePageLimit,
        int cachePageLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(visiblePageLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cachePageLimit, visiblePageLimit);

        PageSize = pageSize;
        VisiblePageLimit = visiblePageLimit;
        CachePageLimit = cachePageLimit;
        _cache = new BoundedPageCache<T>(cachePageLimit);
    }

    public int PageSize { get; }
    public int VisiblePageLimit { get; }
    public int CachePageLimit { get; }
    public int TotalCount { get; private set; }
    public int WindowStartOffset { get; private set; }
    public int WindowEndOffset { get; private set; }
    public int CurrentPageOffset { get; private set; }
    public int CachedPageCount => _cache.Count;
    public int VisiblePageCount => _visibleOffsets.Count;
    public int CachedItemUpperBound => CachePageLimit * PageSize;
    public bool HasPrevious => WindowStartOffset > 0;
    public bool HasNext => WindowEndOffset < TotalCount;
    public int NextOffset => WindowEndOffset;
    public int PreviousOffset => Math.Max(0, WindowStartOffset - PageSize);

    public IReadOnlyList<int> VisibleOffsets => _visibleOffsets.ToArray();

    public void Reset()
    {
        _cache.Clear();
        _visibleOffsets.Clear();
        TotalCount = 0;
        WindowStartOffset = 0;
        WindowEndOffset = 0;
        CurrentPageOffset = 0;
    }

    public void SetTotalCount(int totalCount)
    {
        TotalCount = Math.Max(0, totalCount);

        if (TotalCount == 0)
        {
            CurrentPageOffset = 0;
            return;
        }

        int lastPageOffset = (TotalCount - 1) / PageSize * PageSize;
        CurrentPageOffset = Math.Min(CurrentPageOffset, lastPageOffset);
    }

    public void StorePage(int offset, IReadOnlyList<T> items)
    {
        ValidateOffset(offset);
        ArgumentNullException.ThrowIfNull(items);
        _cache.Set(offset, items);
    }

    public void ShowPage(
        int offset,
        IReadOnlyList<T> items,
        PageWindowInsert insert)
    {
        StorePage(offset, items);

        if (_visibleOffsets.Contains(offset))
        {
            RecalculateWindowBounds();
            return;
        }

        if (insert == PageWindowInsert.Append)
        {
            _visibleOffsets.AddLast(offset);

            while (_visibleOffsets.Count > VisiblePageLimit)
            {
                _visibleOffsets.RemoveFirst();
            }
        }
        else
        {
            _visibleOffsets.AddFirst(offset);

            while (_visibleOffsets.Count > VisiblePageLimit)
            {
                _visibleOffsets.RemoveLast();
            }
        }

        RecalculateWindowBounds();
    }

    public bool TryGetCachedPage(
        int offset,
        out IReadOnlyList<T> items)
    {
        ValidateOffset(offset);
        return _cache.TryGet(offset, out items);
    }

    public IReadOnlyList<T> FlattenVisiblePages()
    {
        var result = new List<T>(VisiblePageCount * PageSize);

        foreach (int offset in _visibleOffsets)
        {
            if (_cache.TryGet(offset, out IReadOnlyList<T> page))
            {
                result.AddRange(page);
            }
        }

        return result;
    }

    public IReadOnlyList<int> GetPrefetchOffsets(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0 || !HasNext)
        {
            return Array.Empty<int>();
        }

        var result = new List<int>(count);
        int offset = NextOffset;

        while (result.Count < count && offset < TotalCount)
        {
            if (!_cache.Contains(offset))
            {
                result.Add(offset);
            }

            offset += PageSize;
        }

        return result;
    }

    public bool UpdateViewport(double logicalVerticalOffset)
    {
        int pageOffset = LibraryRangeTextFormatter.GetPageStartOffset(
            WindowStartOffset,
            logicalVerticalOffset,
            PageSize);

        if (TotalCount > 0)
        {
            int lastPageOffset = (TotalCount - 1) / PageSize * PageSize;
            pageOffset = Math.Min(pageOffset, lastPageOffset);
        }

        if (pageOffset == CurrentPageOffset)
        {
            return false;
        }

        CurrentPageOffset = pageOffset;
        return true;
    }

    private void RecalculateWindowBounds()
    {
        int? start = null;
        int end = 0;

        foreach (int offset in _visibleOffsets)
        {
            if (!_cache.TryGet(offset, out IReadOnlyList<T> page))
            {
                continue;
            }

            start ??= offset;
            end = Math.Max(end, offset + page.Count);
        }

        WindowStartOffset = start ?? 0;
        WindowEndOffset = end;

        if (_visibleOffsets.Count == 1)
        {
            CurrentPageOffset = WindowStartOffset;
        }
        else if (CurrentPageOffset < WindowStartOffset || CurrentPageOffset >= WindowEndOffset)
        {
            CurrentPageOffset = WindowStartOffset;
        }
    }

    private void ValidateOffset(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (offset % PageSize != 0)
        {
            throw new ArgumentException(
                $"Offset must be aligned to page size {PageSize}.",
                nameof(offset));
        }
    }
}
