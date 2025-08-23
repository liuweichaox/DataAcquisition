using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Core.DataStorages;

public abstract class AbstractDataStorage : IDataStorage
{
    protected AbstractDataStorage(string connectionString)
    {
    }

    public abstract Task SaveAsync(DataMessage dataMessage);
    public abstract Task SaveBatchAsync(List<DataMessage> dataPoints);
}