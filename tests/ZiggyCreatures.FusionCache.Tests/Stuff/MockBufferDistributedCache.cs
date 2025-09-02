using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;

namespace FusionCacheTests.Stuff;

#if NET9_0_OR_GREATER
/// <summary>
/// A mock implementation of IBufferDistributedCache for testing buffer-optimized operations.
/// This wraps a regular IDistributedCache and tracks whether buffer methods were used.
/// </summary>
internal class MockBufferDistributedCache : IBufferDistributedCache
{
	private readonly IDistributedCache _innerCache;
	
	public bool BufferMethodsUsed { get; private set; }

	public MockBufferDistributedCache(IDistributedCache innerCache)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
	}

	// IDistributedCache methods (delegate to inner cache)
	public byte[]? Get(string key) => _innerCache.Get(key);

	public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
		=> _innerCache.GetAsync(key, token);

	public void Refresh(string key) => _innerCache.Refresh(key);

	public Task RefreshAsync(string key, CancellationToken token = default)
		=> _innerCache.RefreshAsync(key, token);

	public void Remove(string key) => _innerCache.Remove(key);

	public Task RemoveAsync(string key, CancellationToken token = default)
		=> _innerCache.RemoveAsync(key, token);

	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		=> _innerCache.Set(key, value, options);

	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		=> _innerCache.SetAsync(key, value, options, token);

	// IBufferDistributedCache methods (track usage and convert to byte[])
	public bool TryGet(string key, IBufferWriter<byte> destination)
	{
		BufferMethodsUsed = true;
		var data = _innerCache.Get(key);
		if (data != null)
		{
			destination.Write(data);
			return true;
		}
		return false;
	}

	public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		BufferMethodsUsed = true;
		var data = await _innerCache.GetAsync(key, token).ConfigureAwait(false);
		if (data != null)
		{
			destination.Write(data);
			return true;
		}
		return false;
	}

	public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
	{
		BufferMethodsUsed = true;
		_innerCache.Set(key, value.ToArray(), options);
	}

	public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		BufferMethodsUsed = true;
		await _innerCache.SetAsync(key, value.ToArray(), options, token).ConfigureAwait(false);
	}
}
#endif