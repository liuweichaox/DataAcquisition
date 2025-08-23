using System.Threading.Tasks;

namespace DataAcquisition.Core.Messages;

public interface IMessageService
{
    Task SendAsync(string message);
}