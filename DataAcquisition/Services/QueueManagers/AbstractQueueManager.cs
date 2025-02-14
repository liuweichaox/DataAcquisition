using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAcquisition.Common;

namespace DataAcquisition.Services.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    public AbstractQueueManager(DataStorageFactory dataStorageFactory, DataAcquisitionConfig dataAcquisitionConfig)
    {
        ProcessQueueAsync();
    }
    
    public abstract void EnqueueData(Dictionary<string, object> data);
    public abstract Task ProcessQueueAsync();
    public abstract void Complete();
}