using DataAcquisition.Models;
using DataAcquisition.Services.DataAcquisitionConfigs;
using Samples.Utils;

namespace Samples.Services.DataAcquisitionConfigs;

public class DataAcquisitionConfigService : IDataAcquisitionConfigService
{
    public async Task<List<DataAcquisitionConfig>> GetDataAcquisitionConfigs()
    {
        var dataAcquisitionConfigs = await JsonUtils.LoadAllJsonFilesAsync<DataAcquisitionConfig>("Configs/MetricConfigs");
        return dataAcquisitionConfigs;
    }
}