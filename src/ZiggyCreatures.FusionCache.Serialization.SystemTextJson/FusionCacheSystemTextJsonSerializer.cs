using System.Text.Json;
#if NET9_0_OR_GREATER
using System.Buffers;
using ZiggyCreatures.Caching.Fusion.Serialization;
#endif

namespace ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

/// <summary>
/// An implementation of <see cref="IFusionCacheSerializer"/> which uses the System.Text.Json serializer.
/// </summary>
public class FusionCacheSystemTextJsonSerializer
	: IFusionCacheSerializer
#if NET9_0_OR_GREATER
	, IBufferFusionCacheSerializer
#endif
{
	/// <summary>
	/// The options class for the <see cref="FusionCacheSystemTextJsonSerializer"/> class.
	/// </summary>
	public class Options
	{
		/// <summary>
		/// The optional <see cref="JsonSerializerOptions"/> object to use.
		/// </summary>
		public JsonSerializerOptions? SerializerOptions { get; set; }
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="JsonSerializerOptions"/> object to use.</param>
	public FusionCacheSystemTextJsonSerializer(JsonSerializerOptions? options = null)
	{
		_serializerOptions = options;
	}

	/// <summary>
	/// Creates a new instance of a <see cref="FusionCacheSystemTextJsonSerializer"/> object.
	/// </summary>
	/// <param name="options">The optional <see cref="Options"/> object to use.</param>
	public FusionCacheSystemTextJsonSerializer(Options? options)
		: this(options?.SerializerOptions)
	{
	}

	private readonly JsonSerializerOptions? _serializerOptions;

	/// <inheritdoc />
	public byte[] Serialize<T>(T? obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes<T?>(obj, _serializerOptions);
	}

	/// <inheritdoc />
	public T? Deserialize<T>(byte[] data)
	{
		return JsonSerializer.Deserialize<T>(data, _serializerOptions);
	}

	/// <inheritdoc />
	public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
	{
		return new ValueTask<byte[]>(Serialize(obj));
	}

	/// <inheritdoc />
	public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
	{
		return new ValueTask<T?>(Deserialize<T>(data));
	}

	/// <inheritdoc />
	public override string ToString() => GetType().Name;

#if NET9_0_OR_GREATER
	/// <inheritdoc />
	public void Serialize<T>(T? obj, IBufferWriter<byte> bufferWriter)
	{
		using var writer = new Utf8JsonWriter(bufferWriter);
		JsonSerializer.Serialize<T?>(writer, obj, _serializerOptions);
	}

	/// <inheritdoc />
	public ValueTask SerializeAsync<T>(T? obj, IBufferWriter<byte> bufferWriter, CancellationToken token = default)
	{
		// System.Text.Json doesn't have native async buffer writing for non-stream scenarios,
		// so we use the synchronous version
		Serialize(obj, bufferWriter);
		return ValueTask.CompletedTask;
	}
#endif
}
