namespace Mnemora.Desktop.ViewModels.Library;

/// <summary>
/// Small bounded LRU cache for database pages. The cache owns DTO pages only;
/// visible WPF rows are maintained separately by the view model.
/// </summary>
public sealed class BoundedPageCache<T>
{
    private readonly int _maxPages;
    private readonly Dictionary<int, CacheEntry> _pages = [];
    private readonly LinkedList<int> _lru = [];
    private readonly object _gate = new();

    public BoundedPageCache(int maxPages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);
        _maxPages = maxPages;
    }

    public int MaxPages => _maxPages;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _pages.Count;
            }
        }
    }

    public bool TryGet(int offset, out IReadOnlyList<T> items)
    {
        lock (_gate)
        {
            if (!_pages.TryGetValue(offset, out CacheEntry? entry))
            {
                items = Array.Empty<T>();
                return false;
            }

            Touch(entry);
            items = entry.Items;
            return true;
        }
    }

    public void Set(int offset, IReadOnlyList<T> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentNullException.ThrowIfNull(items);

        lock (_gate)
        {
            if (_pages.TryGetValue(offset, out CacheEntry? existing))
            {
                existing.Items = items;
                Touch(existing);
                return;
            }

            LinkedListNode<int> node = _lru.AddFirst(offset);
            _pages[offset] = new CacheEntry(items, node);

            while (_pages.Count > _maxPages)
            {
                LinkedListNode<int>? last = _lru.Last;
                if (last is null)
                {
                    break;
                }

                _lru.RemoveLast();
                _pages.Remove(last.Value);
            }
        }
    }

    public bool Contains(int offset)
    {
        lock (_gate)
        {
            return _pages.ContainsKey(offset);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _pages.Clear();
            _lru.Clear();
        }
    }

    public IReadOnlyList<int> GetCachedOffsets()
    {
        lock (_gate)
        {
            return _pages.Keys.OrderBy(offset => offset).ToArray();
        }
    }

    private void Touch(CacheEntry entry)
    {
        _lru.Remove(entry.Node);
        _lru.AddFirst(entry.Node);
    }

    private sealed class CacheEntry(
        IReadOnlyList<T> items,
        LinkedListNode<int> node)
    {
        public IReadOnlyList<T> Items { get; set; } = items;
        public LinkedListNode<int> Node { get; } = node;
    }
}
