using System.Threading.Tasks;

namespace DataAcquisition.Services.DataAcquisitions;

public interface IDataAcquisitionService
{
    /// <summary>
    /// 开始采集任务
    /// </summary>
    Task StartCollectionTasks();
}