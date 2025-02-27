namespace DataAcquisition.Core.Messages;

public interface IMessageFactory
{
    IMessageService Create();
}