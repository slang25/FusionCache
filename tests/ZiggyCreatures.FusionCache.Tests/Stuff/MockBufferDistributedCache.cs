using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ZiggyCreatures.FusionCache.Tests.Stuff;

#if NET9_0_OR_GREATER
/// <summary>
/// A mock implementation of IBufferDistributedCache for testing buffer-optimized operations.
/// This wraps a regular IDistributedCache and tracks whether buffer methods were used.
/// </summary>
internal class MockBufferDistributedCache : IBufferDistributedCache
{
	private readonly IDistributedCache _innerCache;
	
	public bool BufferSetCalled { get; private set; }
	public bool BufferGetCalled { get; private set; }
	
	/// <summary>
	/// Returns true if any buffer methods (TryGet or Set) were called.
	/// </summary>
	public bool BufferMethodsUsed => BufferSetCalled || BufferGetCalled;

	public MockBufferDistributedCache()
	{
		_innerCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}

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
		BufferGetCalled = true;
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
		BufferGetCalled = true;
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
		BufferSetCalled = true;
		_innerCache.Set(key, value.ToArray(), options);
	}

	public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		BufferSetCalled = true;
		await _innerCache.SetAsync(key, value.ToArray(), options, token).ConfigureAwait(false);
	}
}
#endif