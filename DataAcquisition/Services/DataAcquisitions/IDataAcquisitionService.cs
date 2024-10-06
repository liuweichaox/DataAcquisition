using System.Threading.Tasks;

namespace DataAcquisition.Services.DataAcquisitions;

public interface IDataAcquisitionService
{
    /// <summary>
    /// 开始采集任务
    /// </summary>
    Task StartCollectionTasks();

    /// <summary>
    /// 处理退出
    /// </summary>
    /// <returns></returns>
    Task HandleExitAsync();
}