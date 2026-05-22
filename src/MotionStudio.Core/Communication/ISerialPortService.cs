using System.IO.Ports;

namespace MotionStudio.Core.Communication;

public interface ISerialPortService
{
    Task<string> SendAndReceiveAsync(
        string portName,
        int baudRate,
        int dataBits,
        Parity parity,
        StopBits stopBits,
        string sendText,
        int receiveTimeoutMs,
        string encodingName,
        CancellationToken token);
}
