using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;
#if NET9_0_OR_GREATER
using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
#endif

namespace ZiggyCreatures.Caching.Fusion.Internals.Distributed;

internal partial class DistributedCacheAccessor
{
	private bool ExecuteOperation(string operationId, string key, Action<CancellationToken> action, string actionDescription, FusionCacheEntryOptions options, CancellationToken token)
	{
		//if (IsCurrentlyUsable(operationId, key) == false)
		//	return false;

		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] before " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);

			RunUtils.RunSyncActionWithTimeout(action, Timeout.InfiniteTimeSpan, true, token: token);

			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] after " + actionDescription, _options.CacheName, _options.InstanceId, operationId, key);
		}
		catch (Exception exc)
		{
			ProcessError(operationId, key, exc, actionDescription);

			// ACTIVITY
			Activity.Current?.SetStatus(ActivityStatusCode.Error, exc.Message);
			Activity.Current?.AddException(exc);

			if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheDistributedCacheException("An error occurred while working with the distributed cache", exc);
				}
			}

			return false;
		}

		return true;
	}

	public bool SetEntry<TValue>(string operationId, string key, IFusionCacheEntry entry, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		if (IsCurrentlyUsable(operationId, key) == false)
			return false;

		token.ThrowIfCancellationRequested();

		// ACTIVITY
		using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedSet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

		// IF FAIL-SAFE IS DISABLED AND DURATION IS <= ZERO -> REMOVE ENTRY (WILL SAVE RESOURCES)
		if (options.IsFailSafeEnabled == false && options.DistributedCacheDuration.GetValueOrDefault(options.Duration) <= TimeSpan.Zero)
		{
			RemoveEntry(operationId, key, options, isBackground, token);
			return true;
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] saving distributed entry", _options.CacheName, _options.InstanceId, operationId, key);

		var distributedEntry = entry.AsDistributedEntry<TValue>(options);

		// SERIALIZATION
		byte[]? data = null;
#if NET9_0_OR_GREATER
		ArrayPoolBufferWriter? bufferWriter = null;
#endif
		try
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] serializing the entry {Entry}", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

#if NET9_0_OR_GREATER
			if (_serializerSupportsBuffers && _serializer is IBufferFusionCacheSerializer bufferSerializer)
			{
				// Use buffer-optimized serialization
				bufferWriter = new ArrayPoolBufferWriter();
				bufferSerializer.Serialize(distributedEntry, bufferWriter);
			}
			else
#endif
			{
				// Use traditional byte[] serialization
				data = _serializer.Serialize(distributedEntry);
			}
		}
		catch (Exception exc)
		{
#if NET9_0_OR_GREATER
			bufferWriter?.Dispose();
#endif
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while serializing an entry {Entry}", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);

			// EVENT
			_events.OnSerializationError(operationId, key);

			//if (options.ReThrowSerializationExceptions)
			//{
			if (_options.ReThrowOriginalExceptions)
			{
				throw;
			}
			else
			{
				throw new FusionCacheSerializationException("An error occurred while serializing a cache value", exc);
			}
			//}

			//data = null;
		}

#if NET9_0_OR_GREATER
		if (bufferWriter is not null && bufferWriter.WrittenCount == 0)
		{
			bufferWriter.Dispose();
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] the entry {Entry} has been serialized to empty buffer, skipping", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

			return false;
		}
#endif

		if (data is null
#if NET9_0_OR_GREATER
		    && bufferWriter is null
#endif
		   )
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] the entry {Entry} has been serialized to null, skipping", _options.CacheName, _options.InstanceId, operationId, key, distributedEntry.ToLogString(_options.IncludeTagsInLogs));

			return false;
		}

		// SAVE TO DISTRIBUTED CACHE
		return ExecuteOperation(
			operationId,
			key,
			_ =>
			{
				var distributedOptions = options.ToDistributedCacheEntryOptions(_options, _logger, operationId, key);

#if NET9_0_OR_GREATER
				if (_supportsBuffers && _cache is IBufferDistributedCache bufferCache)
				{
					// Use buffer-optimized distributed cache
					if (bufferWriter is not null)
					{
						// Both serializer and cache support buffers - most efficient path
						var sequence = bufferWriter.ToReadOnlySequence();
						bufferCache.Set(MaybeProcessCacheKey(key), sequence, distributedOptions);
						bufferWriter.Dispose();
					}
					else
					{
						// Only cache supports buffers - convert byte[] to ReadOnlySequence
						var sequence = new ReadOnlySequence<byte>(data!);
						bufferCache.Set(MaybeProcessCacheKey(key), sequence, distributedOptions);
					}
				}
				else
#endif
				{
					// Use traditional byte[] path
#if NET9_0_OR_GREATER
					if (bufferWriter is not null)
					{
						// Serializer supports buffers but cache doesn't - convert to byte[]
						data = bufferWriter.ToArray();
						bufferWriter.Dispose();
					}
#endif
					_cache.Set(MaybeProcessCacheKey(key), data!, distributedOptions);
				}

				// EVENT
				_events.OnSet(operationId, key);
			},
			"setting entry in distributed" + isBackground.ToString(" (background)"),
			options,
			token
		);
	}

	public (FusionCacheDistributedEntry<TValue>? entry, bool isValid) TryGetEntry<TValue>(string operationId, string key, FusionCacheEntryOptions options, bool hasFallbackValue, TimeSpan? timeout, CancellationToken token)
	{
		// METRIC
		Metrics.CounterDistributedGet.Maybe()?.AddWithCommonTags(1, _options.CacheName, _options.InstanceId!);

		if (IsCurrentlyUsable(operationId, key) == false)
			return (null, false);

		if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
			_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] trying to get entry from distributed", _options.CacheName, _options.InstanceId, operationId, key);

		// ACTIVITY
		using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedGet, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

		// GET FROM DISTRIBUTED CACHE
		byte[]? data;
		try
		{
			timeout ??= options.GetAppropriateDistributedCacheTimeout(hasFallbackValue);
			
#if NET9_0_OR_GREATER
			if (_supportsBuffers && _cache is IBufferDistributedCache bufferCache)
			{
				// Use buffer-optimized path
				data = RunUtils.RunSyncFuncWithTimeout<byte[]?>(
					_ =>
					{
						using var bufferWriter = new ArrayPoolBufferWriter();
						if (bufferCache.TryGet(MaybeProcessCacheKey(key), bufferWriter))
						{
							return bufferWriter.ToArray();
						}
						return null;
					},
					timeout.Value,
					true,
					token: token
				);
			}
			else
#endif
			{
				// Use traditional byte[] path
				data = RunUtils.RunSyncFuncWithTimeout<byte[]?>(
					_ => _cache.Get(MaybeProcessCacheKey(key)),
					timeout.Value,
					true,
					token: token
				);
			}
		}
		catch (Exception exc)
		{
			ProcessError(operationId, key, exc, "getting entry from distributed");

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);

			if (exc is not SyntheticTimeoutException && options.ReThrowDistributedCacheExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheDistributedCacheException("An error occurred while working with the distributed cache", exc);
				}
			}

			data = null;
		}

		if (data is null)
		{
			if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
				_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry not found", _options.CacheName, _options.InstanceId, operationId, key);

			_events.OnMiss(operationId, key, activity);

			return (null, false);
		}

		// DESERIALIZATION
		try
		{
			var entry = _serializer.Deserialize<FusionCacheDistributedEntry<TValue>>(data);
			var isValid = false;
			if (entry is null)
			{
				if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
					_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry not found", _options.CacheName, _options.InstanceId, operationId, key);
			}
			else
			{
				if (entry.IsLogicallyExpired())
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry found (expired) {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));
				}
				else
				{
					if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
						_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] distributed entry found {Entry}", _options.CacheName, _options.InstanceId, operationId, key, entry.ToLogString(_options.IncludeTagsInLogs));

					isValid = true;
				}
			}

			// EVENT
			if (entry is not null)
			{
				_events.OnHit(operationId, key, isValid == false, activity);
			}
			else
			{
				_events.OnMiss(operationId, key, activity);
			}

			return (entry, isValid);
		}
		catch (Exception exc)
		{
			if (_logger?.IsEnabled(_options.SerializationErrorsLogLevel) ?? false)
				_logger.Log(_options.SerializationErrorsLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] an error occurred while deserializing an entry", _options.CacheName, _options.InstanceId, operationId, key);

			// ACTIVITY
			activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
			activity?.AddException(exc);

			// EVENT
			_events.OnDeserializationError(operationId, key);

			if (options.ReThrowSerializationExceptions)
			{
				if (_options.ReThrowOriginalExceptions)
				{
					throw;
				}
				else
				{
					throw new FusionCacheSerializationException("An error occurred while deserializing a cache value", exc);
				}
			}
		}

		// EVENT
		_events.OnMiss(operationId, key, activity);

		return (null, false);
	}

	public bool RemoveEntry(string operationId, string key, FusionCacheEntryOptions options, bool isBackground, CancellationToken token)
	{
		if (IsCurrentlyUsable(operationId, key) == false)
			return false;

		// ACTIVITY
		using var activity = Activities.SourceDistributedLevel.StartActivityWithCommonTags(Activities.Names.DistributedRemove, _options.CacheName, _options.InstanceId!, key, operationId, CacheLevelKind.Distributed);

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): [DC] removing distributed entry", _options.CacheName, _options.InstanceId, operationId, key);

		return ExecuteOperation(
			operationId,
			key,
			_ =>
			{
				_cache.Remove(MaybeProcessCacheKey(key));

				// EVENT
				_events.OnRemove(operationId, key);
			},
			"removing entry from distributed" + isBackground.ToString(" (background)"),
			options,
			token
		);
	}
}
