using DataAcquisition.Models;
using DataAcquisition.Services.DataAcquisitionConfigs;
using WebAppSamples.Utils;

namespace WebAppSamples.Services.DataAcquisitionConfigs;

public class DataAcquisitionConfigService : IDataAcquisitionConfigService
{
    public async Task<List<DataAcquisitionConfig>> GetConfigs()
    {
        var dataAcquisitionConfigs = await JsonUtils.LoadAllJsonFilesAsync<DataAcquisitionConfig>("Configs");
        return dataAcquisitionConfigs;
    }
}