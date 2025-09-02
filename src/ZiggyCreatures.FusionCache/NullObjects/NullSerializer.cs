using ZiggyCreatures.Caching.Fusion.Serialization;
#if NET9_0_OR_GREATER
using System.Buffers;
#endif

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> that implements the null object pattern, meaning that it does nothing.
/// </summary>
public class NullSerializer
	: IFusionCacheSerializer
#if NET9_0_OR_GREATER
	, IBufferFusionCacheSerializer
#endif
{
	/// <inheritdoc/>
	public byte[] Serialize<T>(T? obj)
	{
		return [];
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data)
	{
		return default;
	}

	/// <inheritdoc/>
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>([]);
	}

	/// <inheritdoc/>
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(default(T?));
	}

#if NET9_0_OR_GREATER
	/// <inheritdoc/>
	public void Serialize<T>(T? obj, IBufferWriter<byte> bufferWriter)
	{
		// Do nothing
	}

	/// <inheritdoc/>
	public ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> bufferWriter, CancellationToken token = default)
	{
		return ValueTask.CompletedTask;
	}
#endif
}
