using DataAcquisition.Core.DataAcquisitionConfigs;
using DataAcquisition.Core.Models;
using DataAcquisition.Gateway.Utils;

namespace DataAcquisition.Gateway.Services.DataAcquisitionConfigs;

public class DataAcquisitionConfigService : IDataAcquisitionConfigService
{
    public async Task<List<DataAcquisitionConfig>> GetConfigs()
    {
        var dataAcquisitionConfigs = await JsonUtils.LoadAllJsonFilesAsync<DataAcquisitionConfig>("Configs");
        return dataAcquisitionConfigs;
    }
}