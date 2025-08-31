using System.Collections.Concurrent;

namespace Assignment.Workflow.Engine.Expression;

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache implementation with size limit
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache</typeparam>
/// <typeparam name="TValue">The type of values in the cache</typeparam>
public class LRUCache<TKey, TValue> where TKey : notnull
{
    private readonly int _maxSize;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new();

    public LRUCache(int maxSize)
    {
        if (maxSize <= 0)
            throw new ArgumentException("Max size must be greater than zero", nameof(maxSize));
            
        _maxSize = maxSize;
        _cache = new ConcurrentDictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            lock (_lock)
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }
            
            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Add(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing item and move to front
                existingNode.Value.Value = value;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // Check if we need to evict items
            while (_lruList.Count >= _maxSize)
            {
                EvictLeastRecentlyUsed();
            }

            // Add new item
            var newItem = new CacheItem(key, value);
            var newNode = new LinkedListNode<CacheItem>(newItem);
            
            _lruList.AddFirst(newNode);
            _cache[key] = newNode;
        }
    }

    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
                return value;
            throw new KeyNotFoundException($"Key '{key}' not found in cache");
        }
        set => Add(key, value);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    public int Count => _cache.Count;

    public int MaxSize => _maxSize;

    private void EvictLeastRecentlyUsed()
    {
        if (_lruList.Last != null)
        {
            var lru = _lruList.Last;
            _lruList.RemoveLast();
            _cache.TryRemove(lru.Value.Key, out _);
        }
    }

    private class CacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; set; }

        public CacheItem(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}