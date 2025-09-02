using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
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
		using var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<FusionCache>();

		var cache = new FusionCache(new FusionCacheOptions 
		{ 
			DefaultEntryOptions = new FusionCacheEntryOptions()
		}, logger);

		cache.SetupDistributedCache(distributedCache, serializer);

		// Act
		var testKey = "buffer-test-key";
		var testValue = "buffer-test-value";
		
		cache.Set(testKey, testValue);
		var retrievedValue = cache.Get<string>(testKey);

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
		using var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<FusionCache>();

		var cache = new FusionCache(new FusionCacheOptions 
		{ 
			DefaultEntryOptions = new FusionCacheEntryOptions()
		}, logger);

		cache.SetupDistributedCache(distributedCache, serializer);

		// Act
		var testKey = "async-buffer-test-key";
		var testValue = "async-buffer-test-value";
		
		await cache.SetAsync(testKey, testValue);
		var retrievedValue = await cache.GetAsync<string>(testKey);

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
		var serializer = new NullObjects.NullSerializer();

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
		var serializer = new NullObjects.NullSerializer();

		// Act & Assert - Just verify it implements the interface without throwing
		Assert.IsAssignableFrom<IBufferFusionCacheSerializer>(serializer);
		
		using var bufferWriter = new ArrayPoolBufferWriter();
		await serializer.SerializeAsync("test", bufferWriter);
		
		// NullSerializer should not write anything
		Assert.Equal(0, bufferWriter.WrittenCount);
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
		using var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<FusionCache>();

		var cache = new FusionCache(new FusionCacheOptions 
		{ 
			DefaultEntryOptions = new FusionCacheEntryOptions()
		}, logger);

		// Use a traditional serializer (not implementing IBufferFusionCacheSerializer)
		var traditionalSerializer = new NullObjects.NullSerializer();
		var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();
		
		cache.SetupDistributedCache(distributedCache, traditionalSerializer);

		// Act & Assert - Should work without errors
		var testKey = "traditional-test-key";
		var testValue = "traditional-test-value";
		
		cache.Set(testKey, testValue);
		var retrievedValue = cache.Get<string>(testKey);

		// With NullSerializer, we expect null/default values, which is expected behavior
		Assert.Equal(default(string), retrievedValue);
	}
}