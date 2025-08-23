using System.Threading.Tasks;

namespace DataAcquisition.Core.Messages;

public interface IMessage
{
    Task SendAsync(string message);
}