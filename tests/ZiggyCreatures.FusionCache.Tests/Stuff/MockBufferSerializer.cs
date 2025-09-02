#if NET9_0_OR_GREATER
using System.Buffers;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.FusionCache.Tests.Stuff;

/// <summary>
/// A mock implementation of <see cref="IBufferFusionCacheSerializer"/> for testing buffer serialization functionality.
/// </summary>
public class MockBufferSerializer : IBufferFusionCacheSerializer
{
	/// <inheritdoc/>
	public byte[] Serialize<T>(T? obj)
	{
		// Simple serialization - just convert to string and then to bytes
		var str = obj?.ToString() ?? string.Empty;
		return System.Text.Encoding.UTF8.GetBytes(str);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data)
	{
		// Simple deserialization - convert bytes to string, then try to convert to T
		var str = System.Text.Encoding.UTF8.GetString(data);
		if (typeof(T) == typeof(string))
		{
			return (T?)(object?)str;
		}
		return default(T);
	}

	/// <inheritdoc/>
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc/>
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}

	/// <inheritdoc/>
	public void Serialize<T>(T? obj, IBufferWriter<byte> bufferWriter)
	{
		var str = obj?.ToString() ?? string.Empty;
		var bytes = System.Text.Encoding.UTF8.GetBytes(str);
		bufferWriter.Write(bytes);
	}

	/// <inheritdoc/>
	public ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> bufferWriter, CancellationToken token = default)
	{
		Serialize(obj, bufferWriter);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public override string ToString() => GetType().Name;
}
#endif