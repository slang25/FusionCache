using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace ZiggyCreatures.FusionCache.Tests
{
    // Mock implementation of IBufferDistributedCache for testing
    public class MockBufferDistributedCache : IBufferDistributedCache
    {
        private readonly Dictionary<string, byte[]> _cache = new();

        public byte[]? Get(string key) => _cache.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _cache.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => _cache[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        // IBufferDistributedCache methods
        public bool TryGet(string key, IBufferWriter<byte> destination)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                destination.Write(value);
                return true;
            }
            return false;
        }

        public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
        {
            await Task.Yield(); // Simulate async operation
            return TryGet(key, destination);
        }

        public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
        {
            _cache[key] = value.ToArray();
        }

        public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            await Task.Yield(); // Simulate async operation
            Set(key, value, options);
        }

        public bool UsedBufferMethods { get; set; }
    }
}