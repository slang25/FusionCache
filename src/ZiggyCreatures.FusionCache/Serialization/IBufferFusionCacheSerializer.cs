#if NET9_0_OR_GREATER
using System.Buffers;

namespace ZiggyCreatures.Caching.Fusion.Serialization;

/// <summary>
/// An extended serializer interface that provides buffer-optimized serialization methods for improved memory efficiency in .NET 9.0+.
/// This interface extends <see cref="IFusionCacheSerializer"/> with <see cref="IBufferWriter{T}"/> support.
/// </summary>
public interface IBufferFusionCacheSerializer : IFusionCacheSerializer
{
	/// <summary>
	/// Serializes the specified <paramref name="obj"/> directly to the provided <see cref="IBufferWriter{T}"/>.
	/// This method avoids intermediate byte array allocations for improved memory efficiency.
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="bufferWriter">The buffer writer to write the serialized data to.</param>
	void Serialize<T>(T? obj, IBufferWriter<byte> bufferWriter);

	/// <summary>
	/// Asynchronously serializes the specified <paramref name="obj"/> directly to the provided <see cref="IBufferWriter{T}"/>.
	/// This method avoids intermediate byte array allocations for improved memory efficiency.
	/// </summary>
	/// <typeparam name="T">The type of the <paramref name="obj"/> parameter.</typeparam>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="bufferWriter">The buffer writer to write the serialized data to.</param>
	/// <param name="token">The cancellation token.</param>
	/// <returns>A task representing the asynchronous serialization operation.</returns>
	ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> bufferWriter, CancellationToken token = default);
}
#endif