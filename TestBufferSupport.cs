using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Testing IBufferDistributedCache support in FusionCache...");

        // Create a mock buffer-distributed cache
        var mockCache = new MockBufferDistributedCache();
        
        // Create a simple serializer
        var serializer = new FusionCacheSystemTextJsonSerializer();
        
        // Create FusionCache options
        var options = new FusionCacheOptions();
        
        // Create FusionCache instance
        var cache = new FusionCache(options, NullLogger<FusionCache>.Instance);
        cache.SetupDistributedCache(mockCache, serializer);

        // Test value
        var testKey = "test-key";
        var testValue = "Hello, IBufferDistributedCache!";

        Console.WriteLine($"Setting value: {testValue}");
        
        // Set a value
        await cache.SetAsync(testKey, testValue);
        
        Console.WriteLine("Value set successfully");
        
        // Get the value back
        var retrievedValue = await cache.GetOrDefaultAsync<string>(testKey);
        
        Console.WriteLine($"Retrieved value: {retrievedValue}");
        
        // Verify the values match
        if (testValue == retrievedValue)
        {
            Console.WriteLine("✅ Test PASSED: Values match!");
        }
        else
        {
            Console.WriteLine("❌ Test FAILED: Values don't match!");
        }
        
        Console.WriteLine("Test completed.");
    }
}