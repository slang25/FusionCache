using System.Runtime.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using MemoryPack;
using MessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;
using ZiggyCreatures.Caching.Fusion.Serialization.NeueccMessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;
using ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
#if NET9_0_OR_GREATER
using System.Buffers;
using ZiggyCreatures.Caching.Fusion.Internals;
#endif

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[DataContract]
[MessagePackObject]
[MemoryPackable]
public partial class SampleModel
{
	private static readonly Random _MyRandom = new(2110);

	[DataMember(Order = 1)]
	[Key(0)]
	public string? Name { get; set; }
	[DataMember(Order = 2)]
	[Key(1)]
	public int Age { get; set; }
	[DataMember(Order = 3)]
	[Key(2)]
	public DateTime Date { get; set; }
	[DataMember(Order = 4)]
	[Key(3)]
	public List<int> FavoriteNumbers { get; set; } = [];

	public static SampleModel GenerateRandom()
	{
		var model = new SampleModel
		{
			Name = Guid.NewGuid().ToString("N"),
			Age = _MyRandom.Next(1, 100),
			Date = DateTime.UtcNow,
		};
		for (int i = 0; i < 10; i++)
		{
			model.FavoriteNumbers.Add(_MyRandom.Next(1, 1000));
		}
		return model;
	}
}

[Config(typeof(Config))]
public class SerializersBenchmark
{
	public class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
			AddDiagnoser(MemoryDiagnoser.Default);
			AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByMethod);
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
			WithOrderer(new DefaultOrderer(summaryOrderPolicy: SummaryOrderPolicy.FastestToSlowest));
			WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default.WithMaxParameterColumnWidth(50));
		}
	}

	[ParamsSource(nameof(GetSerializers))]
	public IFusionCacheSerializer Serializer = null!;
	protected List<SampleModel> _Models = [];
	protected byte[] _Blob = null!;
#if NET9_0_OR_GREATER
	protected ReadOnlySequence<byte> _BufferBlob;
#endif

	[GlobalSetup]
	public void Setup()
	{
		for (int i = 0; i < 1000; i++)
		{
			_Models.Add(SampleModel.GenerateRandom());
		}

		_Blob = Serializer.Serialize(_Models);
#if NET9_0_OR_GREATER
		_BufferBlob = new ReadOnlySequence<byte>(_Blob);
#endif
	}

	[Benchmark]
	public void Serialize()
	{
		Serializer.Serialize(_Models);
	}

	[Benchmark]
	public void Deserialize()
	{
		Serializer.Deserialize<List<SampleModel>>(_Blob);
	}

	[Benchmark]
	public async Task SerializeAsync()
	{
		await Serializer.SerializeAsync(_Models).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task DeserializeAsync()
	{
		await Serializer.DeserializeAsync<List<SampleModel>>(_Blob).ConfigureAwait(false);
	}

#if NET9_0_OR_GREATER
	[Benchmark]
	public void SerializeBuffer()
	{
		if (Serializer is IBufferFusionCacheSerializer bufferSerializer)
		{
			using var bufferWriter = new ArrayPoolBufferWriter();
			bufferSerializer.Serialize(_Models, bufferWriter);
		}
		else
		{
			// Fallback to traditional serialization for fair comparison
			Serializer.Serialize(_Models);
		}
	}

	[Benchmark]
	public void DeserializeBuffer()
	{
		if (Serializer is IBufferFusionCacheSerializer bufferSerializer)
		{
			bufferSerializer.Deserialize<List<SampleModel>>(_BufferBlob);
		}
		else
		{
			// Fallback to traditional deserialization for fair comparison
			Serializer.Deserialize<List<SampleModel>>(_Blob);
		}
	}

	[Benchmark]
	public async Task SerializeBufferAsync()
	{
		if (Serializer is IBufferFusionCacheSerializer bufferSerializer)
		{
			using var bufferWriter = new ArrayPoolBufferWriter();
			await bufferSerializer.SerializeAsync(_Models, bufferWriter).ConfigureAwait(false);
		}
		else
		{
			// Fallback to traditional serialization for fair comparison
			await Serializer.SerializeAsync(_Models).ConfigureAwait(false);
		}
	}

	[Benchmark]
	public async Task DeserializeBufferAsync()
	{
		if (Serializer is IBufferFusionCacheSerializer bufferSerializer)
		{
			await bufferSerializer.DeserializeAsync<List<SampleModel>>(_BufferBlob).ConfigureAwait(false);
		}
		else
		{
			// Fallback to traditional deserialization for fair comparison
			await Serializer.DeserializeAsync<List<SampleModel>>(_Blob).ConfigureAwait(false);
		}
	}
#endif

	public static IEnumerable<IFusionCacheSerializer> GetSerializers()
	{
		yield return new FusionCacheCysharpMemoryPackSerializer();
		yield return new FusionCacheNeueccMessagePackSerializer();
		yield return new FusionCacheNewtonsoftJsonSerializer();
		yield return new FusionCacheProtoBufNetSerializer();
		yield return new FusionCacheServiceStackJsonSerializer();
		yield return new FusionCacheSystemTextJsonSerializer();
	}
}
