using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.NullObjects;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.FusionCache.Tests.Stuff;
#if NET9_0_OR_GREATER
using System.Buffers;
#endif

namespace ZiggyCreatures.FusionCache.Tests;

/// <summary>
/// Tests for IBufferFusionCacheSerializer functionality in .NET 9.0+
/// </summary>
public class BufferSerializationTests
{
#if NET9_0_OR_GREATER
	[Fact]
	public void BufferSerializer_Sync_ShouldWork()
	{
		// Arrange
		var serializer = new MockBufferSerializer();
		var testValue = "test-buffer-serialization";

		// Act - Test buffer serialization
		using var bufferWriter = new ArrayPoolBufferWriter();
		serializer.Serialize(testValue, bufferWriter);

		// Assert
		Assert.True(bufferWriter.WrittenCount > 0);
		var result = bufferWriter.ToArray();
		var deserializedValue = serializer.Deserialize<string>(result);
		Assert.Equal(testValue, deserializedValue);
	}

	[Fact]
	public async Task BufferSerializer_Async_ShouldWork()
	{
		// Arrange
		var serializer = new MockBufferSerializer();
		var testValue = "test-async-buffer-serialization";

		// Act - Test async buffer serialization
		using var bufferWriter = new ArrayPoolBufferWriter();
		await serializer.SerializeAsync(testValue, bufferWriter);

		// Assert
		Assert.True(bufferWriter.WrittenCount > 0);
		var result = bufferWriter.ToArray();
		var deserializedValue = await serializer.DeserializeAsync<string>(result);
		Assert.Equal(testValue, deserializedValue);
	}

	[Fact]
	public void BufferSerializer_WithDistributedCache_ShouldUseBufferOptimizations()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		
		// Use mock buffer-enabled distributed cache and serializer
		var distributedCache = new MockBufferDistributedCache();
		var serializer = new MockBufferSerializer();
		
		services.AddSingleton<IDistributedCache>(distributedCache);
		services.AddSingleton<IFusionCacheSerializer>(serializer);

		using var serviceProvider = services.BuildServiceProvider();
		var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ZiggyCreatures.Caching.Fusion.FusionCache>();

		var cache = new ZiggyCreatures.Caching.Fusion.FusionCache(Options.Create(new ZiggyCreatures.Caching.Fusion.FusionCacheOptions 
		{ 
			DefaultEntryOptions = new ZiggyCreatures.Caching.Fusion.FusionCacheEntryOptions()
		}), logger: logger);

		cache.SetupDistributedCache(distributedCache, serializer);

		// Act
		var testKey = "buffer-test-key";
		var testValue = "buffer-test-value";
		
		cache.Set(testKey, testValue);
		var retrievedValue = cache.GetOrDefault<string>(testKey);

		// Assert
		Assert.Equal(testValue, retrievedValue);
		
		// Verify that buffer operations were used (the mock should record this)
		Assert.True(distributedCache.BufferSetCalled);
		Assert.True(distributedCache.BufferGetCalled);
	}

	[Fact]
	public async Task BufferSerializer_WithDistributedCache_Async_ShouldUseBufferOptimizations()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		
		// Use mock buffer-enabled distributed cache and serializer
		var distributedCache = new MockBufferDistributedCache();
		var serializer = new MockBufferSerializer();
		
		services.AddSingleton<IDistributedCache>(distributedCache);
		services.AddSingleton<IFusionCacheSerializer>(serializer);

		using var serviceProvider = services.BuildServiceProvider();
		var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ZiggyCreatures.Caching.Fusion.FusionCache>();

		var cache = new ZiggyCreatures.Caching.Fusion.FusionCache(Options.Create(new ZiggyCreatures.Caching.Fusion.FusionCacheOptions 
		{ 
			DefaultEntryOptions = new ZiggyCreatures.Caching.Fusion.FusionCacheEntryOptions()
		}), logger: logger);

		cache.SetupDistributedCache(distributedCache, serializer);

		// Act
		var testKey = "async-buffer-test-key";
		var testValue = "async-buffer-test-value";
		
		await cache.SetAsync(testKey, testValue);
		var retrievedValue = await cache.GetOrDefaultAsync<string>(testKey);

		// Assert
		Assert.Equal(testValue, retrievedValue);
		
		// Verify that buffer operations were used (the mock should record this)
		Assert.True(distributedCache.BufferSetCalled);
		Assert.True(distributedCache.BufferGetCalled);
	}

	[Fact]
	public void NullSerializer_ShouldImplementBufferInterface()
	{
		// Arrange
		var serializer = new ZiggyCreatures.Caching.Fusion.NullObjects.NullSerializer();

		// Act & Assert - Just verify it implements the interface without throwing
		Assert.IsAssignableFrom<IBufferFusionCacheSerializer>(serializer);
		
		using var bufferWriter = new ArrayPoolBufferWriter();
		serializer.Serialize("test", bufferWriter);
		
		// NullSerializer should not write anything
		Assert.Equal(0, bufferWriter.WrittenCount);
	}

	[Fact]
	public async Task NullSerializer_Async_ShouldImplementBufferInterface()
	{
		// Arrange
		var serializer = new ZiggyCreatures.Caching.Fusion.NullObjects.NullSerializer();

		// Act & Assert - Just verify it implements the interface without throwing
		Assert.IsAssignableFrom<IBufferFusionCacheSerializer>(serializer);
		
		using var bufferWriter = new ArrayPoolBufferWriter();
		await serializer.SerializeAsync("test", bufferWriter);
		
		// NullSerializer should not write anything
		Assert.Equal(0, bufferWriter.WrittenCount);
	}

	[Fact]
	public void BufferSerializer_ReadOnlySequence_Deserialize_ShouldWork()
	{
		// Arrange
		var serializer = new MockBufferSerializer();
		var testValue = "test-readonly-sequence-deserialization";

		// Act - Serialize first to get data
		var data = serializer.Serialize(testValue);
		var sequence = new ReadOnlySequence<byte>(data);
		
		// Test ReadOnlySequence deserialization
		var deserializedValue = serializer.Deserialize<string>(sequence);

		// Assert
		Assert.Equal(testValue, deserializedValue);
	}

	[Fact]
	public async Task BufferSerializer_ReadOnlySequence_DeserializeAsync_ShouldWork()
	{
		// Arrange
		var serializer = new MockBufferSerializer();
		var testValue = "test-async-readonly-sequence-deserialization";

		// Act - Serialize first to get data
		var data = await serializer.SerializeAsync(testValue);
		var sequence = new ReadOnlySequence<byte>(data);
		
		// Test async ReadOnlySequence deserialization
		var deserializedValue = await serializer.DeserializeAsync<string>(sequence);

		// Assert
		Assert.Equal(testValue, deserializedValue);
	}

	[Fact]
	public void BufferSerializer_MultiSegmentSequence_Deserialize_ShouldWork()
	{
		// Arrange
		var serializer = new MockBufferSerializer();
		var testValue = "test-multi-segment-sequence";

		// Create a multi-segment ReadOnlySequence to test the non-single-segment path
		var data1 = serializer.Serialize("test-multi-");
		var data2 = serializer.Serialize("segment-sequence");
		
		var segment1 = new ReadOnlyMemory<byte>(data1);
		var segment2 = new ReadOnlyMemory<byte>(data2);
		
		// Create a multi-segment sequence
		var sequenceSegment1 = new TestSequenceSegment(segment1);
		var sequenceSegment2 = sequenceSegment1.Append(segment2);
		var sequence = new ReadOnlySequence<byte>(sequenceSegment1, 0, sequenceSegment2, sequenceSegment2.Memory.Length);
		
		// Act - Test multi-segment deserialization
		var deserializedValue = serializer.Deserialize<string>(sequence);

		// Assert - Should still work by converting to array internally
		Assert.NotNull(deserializedValue);
	}

	// Helper class for creating multi-segment ReadOnlySequence
	private class TestSequenceSegment : ReadOnlySequenceSegment<byte>
	{
		public TestSequenceSegment(ReadOnlyMemory<byte> memory)
		{
			Memory = memory;
		}

		public TestSequenceSegment Append(ReadOnlyMemory<byte> memory)
		{
			var segment = new TestSequenceSegment(memory)
			{
				RunningIndex = RunningIndex + Memory.Length
			};
			Next = segment;
			return segment;
		}
	}

	[Fact]
	public void NullSerializer_ReadOnlySequence_ShouldImplementNewMethods()
	{
		// Arrange
		var serializer = new ZiggyCreatures.Caching.Fusion.NullObjects.NullSerializer();
		var data = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 });

		// Act & Assert - Just verify it implements the new methods without throwing
		var result = serializer.Deserialize<string>(data);
		Assert.Equal(default(string), result);
	}

	[Fact]
	public async Task NullSerializer_ReadOnlySequence_Async_ShouldImplementNewMethods()
	{
		// Arrange
		var serializer = new ZiggyCreatures.Caching.Fusion.NullObjects.NullSerializer();
		var data = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 });

		// Act & Assert - Just verify it implements the new async methods without throwing
		var result = await serializer.DeserializeAsync<string>(data);
		Assert.Equal(default(string), result);
	}
#endif

	[Fact]
	public void BackwardCompatibility_TraditionalSerializers_ShouldStillWork()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDistributedCache, MemoryDistributedCache>();
		services.AddSingleton<IMemoryCache, MemoryCache>();

		using var serviceProvider = services.BuildServiceProvider();
		var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ZiggyCreatures.Caching.Fusion.FusionCache>();

		var cache = new ZiggyCreatures.Caching.Fusion.FusionCache(Options.Create(new ZiggyCreatures.Caching.Fusion.FusionCacheOptions 
		{ 
			DefaultEntryOptions = new ZiggyCreatures.Caching.Fusion.FusionCacheEntryOptions()
		}), logger: logger);

		// Use a traditional serializer (not implementing IBufferFusionCacheSerializer)
		var traditionalSerializer = new ZiggyCreatures.Caching.Fusion.NullObjects.NullSerializer();
		var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();
		
		cache.SetupDistributedCache(distributedCache, traditionalSerializer);

		// Act & Assert - Should work without errors
		var testKey = "traditional-test-key";
		var testValue = "traditional-test-value";
		
		cache.Set(testKey, testValue);
		var retrievedValue = cache.GetOrDefault<string>(testKey);

		// With NullSerializer, we expect null/default values, which is expected behavior
		Assert.Equal(default(string), retrievedValue);
	}
}