namespace MotionStudio.Core.Communication;

public interface ITcpClientService
{
    Task<string> SendAndReceiveAsync(
        string host,
        int port,
        string sendText,
        int receiveTimeoutMs,
        string encodingName,
        CancellationToken token);
}
