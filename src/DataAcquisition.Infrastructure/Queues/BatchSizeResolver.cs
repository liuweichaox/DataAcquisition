using System;
using System.Collections.Concurrent;
using System.Linq;
using DataAcquisition.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.Queues;

internal sealed class BatchSizeResolver
{
    private readonly ConcurrentDictionary<string, int> _cache = new();
    private readonly IDeviceConfigService _deviceConfigService;
    private readonly ILogger _logger;

    public BatchSizeResolver(IDeviceConfigService deviceConfigService, ILogger logger)
    {
        _deviceConfigService = deviceConfigService;
        _logger = logger;
    }

    public int GetBatchSize(string? plcCode, string? channelCode, string measurement)
    {
        var cacheKey = $"{plcCode ?? "unknown"}:{channelCode ?? "unknown"}:{measurement}";

        return _cache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                var configs = _deviceConfigService.GetConfigs().GetAwaiter().GetResult();
                var channel = configs
                    .FirstOrDefault(c => c.PlcCode == plcCode)
                    ?.Channels?.FirstOrDefault(ch => ch.ChannelCode == channelCode && ch.Measurement == measurement);
                return channel?.BatchSize > 0 ? channel.BatchSize : 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取 BatchSize 配置失败，使用默认值 1: {CacheKey}", cacheKey);
                return 1;
            }
        });
    }
}
