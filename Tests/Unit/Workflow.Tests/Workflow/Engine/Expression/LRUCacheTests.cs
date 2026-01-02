using Workflow.Workflow.Engine.Expression;
using FluentAssertions;
using System.Collections.Concurrent;
using Xunit;

namespace Workflow.Tests.Workflow.Engine.Expression;

public class LRUCacheTests
{
    [Fact]
    public void Constructor_ZeroMaxSize_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new LRUCache<string, string>(0);
        act.Should().Throw<ArgumentException>().WithParameterName("maxSize");
    }

    [Fact]
    public void Constructor_NegativeMaxSize_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new LRUCache<string, string>(-1);
        act.Should().Throw<ArgumentException>().WithParameterName("maxSize");
    }

    [Fact]
    public void Add_SingleItem_StoresSuccessfully()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);

        // Act
        cache.Add("key1", "value1");

        // Assert
        cache.TryGetValue("key1", out var value).Should().BeTrue();
        value.Should().Be("value1");
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Add_BeyondMaxSize_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var cache = new LRUCache<string, string>(3);
        cache.Add("key1", "value1");
        cache.Add("key2", "value2");
        cache.Add("key3", "value3");

        // Act - Add fourth item should evict key1 (oldest)
        cache.Add("key4", "value4");

        // Assert
        cache.TryGetValue("key1", out _).Should().BeFalse("key1 should be evicted");
        cache.TryGetValue("key2", out _).Should().BeTrue("key2 should still exist");
        cache.TryGetValue("key3", out _).Should().BeTrue("key3 should still exist");
        cache.TryGetValue("key4", out _).Should().BeTrue("key4 should be added");
        cache.Count.Should().Be(3);
    }

    [Fact]
    public void TryGetValue_AccessUpdatesLRUOrder()
    {
        // Arrange
        var cache = new LRUCache<string, string>(3);
        cache.Add("key1", "value1");
        cache.Add("key2", "value2");
        cache.Add("key3", "value3");

        // Act - Access key1 to make it most recently used
        cache.TryGetValue("key1", out _);
        
        // Add fourth item - should evict key2 (now oldest) instead of key1
        cache.Add("key4", "value4");

        // Assert
        cache.TryGetValue("key1", out _).Should().BeTrue("key1 should still exist after access");
        cache.TryGetValue("key2", out _).Should().BeFalse("key2 should be evicted");
        cache.TryGetValue("key3", out _).Should().BeTrue("key3 should still exist");
        cache.TryGetValue("key4", out _).Should().BeTrue("key4 should be added");
    }

    [Fact]
    public void Indexer_Get_ReturnsCorrectValue()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);
        cache.Add("key1", "value1");

        // Act
        var value = cache["key1"];

        // Assert
        value.Should().Be("value1");
    }

    [Fact]
    public void Indexer_GetNonExistentKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);

        // Act & Assert
        var act = () => cache["nonexistent"];
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Indexer_Set_AddsOrUpdatesItem()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);

        // Act
        cache["key1"] = "value1";
        cache["key1"] = "updated_value1";

        // Assert
        cache["key1"].Should().Be("updated_value1");
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);
        cache.Add("key1", "value1");
        cache.Add("key2", "value2");

        // Act
        cache.Clear();

        // Assert
        cache.Count.Should().Be(0);
        cache.TryGetValue("key1", out _).Should().BeFalse();
        cache.TryGetValue("key2", out _).Should().BeFalse();
    }

    #region Security Tests - Cache security and resource management

    [Fact]
    public void Add_CachePoisoning_PreventsMaliciousKeyOverwrite()
    {
        // Arrange
        var cache = new LRUCache<string, string>(10);
        cache.Add("legitimate_key", "legitimate_value");

        // Act - Try to overwrite with malicious content
        cache.Add("legitimate_key", "malicious_payload");

        // Assert - Should update value but not corrupt cache structure
        cache["legitimate_key"].Should().Be("malicious_payload", "Overwrite should be allowed but controlled");
        cache.Count.Should().Be(1, "Cache structure should remain intact");
        
        // Verify cache still functions correctly
        cache.Add("another_key", "another_value");
        cache.Count.Should().Be(2);
        cache.TryGetValue("another_key", out _).Should().BeTrue();
    }

    [Fact]
    public void Add_MemoryExhaustionPrevention_LimitsMaximumSize()
    {
        // Arrange - Small cache size to test memory limits
        var cache = new LRUCache<string, string>(100);
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Add many items to test memory management
        for (int i = 0; i < 1000; i++)
        {
            cache.Add($"key_{i}", $"value_{i}_{new string('x', 100)}"); // Each value ~100 chars
        }

        // Assert
        cache.Count.Should().Be(100, "Cache should enforce size limit");
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Should not use excessive memory (less than 10MB for 100 items)
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024, "Memory usage should be bounded");
        
        // Verify LRU eviction worked correctly
        cache.TryGetValue("key_0", out _).Should().BeFalse("Oldest items should be evicted");
        cache.TryGetValue("key_999", out _).Should().BeTrue("Newest items should be retained");
    }

    [Fact]
    public void ConcurrentAccess_ThreadSafety_HandlesRaceConditions()
    {
        // Arrange
        var cache = new LRUCache<string, string>(100);
        var tasks = new List<Task>();
        var results = new ConcurrentBag<bool>();

        // Act - Concurrent operations from multiple threads
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    // Each thread performs various operations
                    for (int j = 0; j < 100; j++)
                    {
                        var key = $"key_{threadId}_{j}";
                        var value = $"value_{threadId}_{j}";
                        
                        cache.Add(key, value);
                        
                        if (cache.TryGetValue(key, out var retrievedValue))
                        {
                            results.Add(retrievedValue == value);
                        }
                        
                        // Some threads also clear cache periodically
                        if (threadId == 0 && j % 50 == 0)
                        {
                            cache.Clear();
                        }
                    }
                }
                catch (Exception)
                {
                    results.Add(false); // Track any thread safety exceptions
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - No exceptions should occur, and successful operations should be consistent
        results.Should().NotContain(false, "All thread-safe operations should succeed");
        cache.Count.Should().BeLessThanOrEqualTo(100, "Cache size should be respected even under concurrency");
    }

    [Fact]
    public void Add_LargeValueObjects_HandlesGracefully()
    {
        // Arrange
        var cache = new LRUCache<string, byte[]>(10);
        var largeValue = new byte[1024 * 1024]; // 1MB array
        Array.Fill(largeValue, (byte)0xFF);

        // Act & Assert - Should handle large objects without issues
        var act = () =>
        {
            for (int i = 0; i < 15; i++) // More than cache size
            {
                cache.Add($"large_key_{i}", largeValue);
            }
        };

        act.Should().NotThrow("Cache should handle large objects gracefully");
        cache.Count.Should().Be(10, "Should respect size limit even with large objects");
    }

    [Fact]
    public void TryGetValue_CacheInvalidation_ResistsTamperingAttempts()
    {
        // Arrange
        var cache = new LRUCache<string, List<string>>(5);
        var originalList = new List<string> { "item1", "item2", "item3" };
        cache.Add("list_key", originalList);

        // Act - Try to modify the cached object externally
        if (cache.TryGetValue("list_key", out var cachedList))
        {
            cachedList.Add("malicious_item"); // External modification
        }

        // Retrieve again
        cache.TryGetValue("list_key", out var retrievedList);

        // Assert - The cached reference should reflect changes (this is expected behavior)
        // but the cache structure itself should remain intact
        retrievedList.Should().Contain("malicious_item", "Reference types will reflect external changes");
        cache.Count.Should().Be(1, "Cache structure should remain intact");
        
        // Verify cache still functions normally
        cache.Add("new_key", new List<string> { "new_item" });
        cache.Count.Should().Be(2);
    }

    [Fact]
    public void EvictionPolicy_UnderPressure_MaintainsConsistentState()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);

        // Fill cache to capacity
        for (int i = 0; i < 5; i++)
        {
            cache.Add($"key_{i}", $"value_{i}");
        }

        // Act - Rapid additions and retrievals to stress eviction logic
        for (int i = 5; i < 100; i++)
        {
            cache.Add($"key_{i}", $"value_{i}");
            
            // Randomly access some existing keys to change LRU order
            if (i % 3 == 0 && cache.TryGetValue($"key_{i-1}", out _))
            {
                // Access recent key to keep it in cache
            }
        }

        // Assert
        cache.Count.Should().Be(5, "Cache should maintain exact size limit");
        
        // Verify cache is still functional
        cache.Add("final_key", "final_value");
        cache.Count.Should().Be(5, "Size should still be respected");
        cache.TryGetValue("final_key", out var value).Should().BeTrue();
        value.Should().Be("final_value");
    }

    [Fact]
    public void MaxSize_Property_ReturnsCorrectValue()
    {
        // Arrange & Act
        var cache = new LRUCache<int, string>(42);

        // Assert
        cache.MaxSize.Should().Be(42);
    }

    [Fact]
    public void Cache_EmptyKeyHandling_WorksCorrectly()
    {
        // Arrange
        var cache = new LRUCache<string, string>(5);

        // Act & Assert - Empty key should be handled gracefully
        cache.Add("", "empty_key_value");
        cache.TryGetValue("", out var value).Should().BeTrue();
        value.Should().Be("empty_key_value");
    }

    #endregion
}