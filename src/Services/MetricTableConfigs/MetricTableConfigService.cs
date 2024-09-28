using DynamicPLCDataCollector.Utils;

namespace DynamicPLCDataCollector.Services.MetricTableConfigs;

public class MetricTableConfigService : IMetricTableConfigService
{
    public async Task<List<MetricTableConfig>> GetMetricTableConfigs()
    {
        var metricTableConfigs = await JsonUtils.LoadAllJsonFilesAsync<MetricTableConfig>("Configs/MetricConfigs");
        return metricTableConfigs;
    }
}