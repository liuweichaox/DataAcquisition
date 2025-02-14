using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAcquisition.Common;

namespace DataAcquisition.Services.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    public AbstractQueueManager(DataStorageFactory dataStorageFactory, DataAcquisitionConfig dataAcquisitionConfig)
    {
        Task.Run((Func<Task>)ProcessQueue);
    }
    public abstract Task ProcessQueue();
    
    public abstract void EnqueueData(Dictionary<string, object> data);
    public abstract void Complete();
}