using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Core.DataStorages;

public abstract class DataStorage : IDataStorage
{
    protected DataStorage(string connectionString)
    {
    }

    public abstract Task SaveAsync(DataMessage dataMessage);
    public abstract Task SaveBatchAsync(List<DataMessage> dataPoints);
}