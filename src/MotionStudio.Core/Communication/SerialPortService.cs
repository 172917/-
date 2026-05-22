using System.IO.Ports;
using System.Text;

namespace MotionStudio.Core.Communication;

public sealed class SerialPortService : ISerialPortService
{
    public async Task<string> SendAndReceiveAsync(
        string portName,
        int baudRate,
        int dataBits,
        Parity parity,
        StopBits stopBits,
        string sendText,
        int receiveTimeoutMs,
        string encodingName,
        CancellationToken token)
    {
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var encoding = Encoding.GetEncoding(encodingName);
            using var serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = Math.Max(100, receiveTimeoutMs),
                WriteTimeout = Math.Max(100, receiveTimeoutMs),
                Encoding = encoding
            };

            serialPort.Open();
            serialPort.Write(sendText ?? string.Empty);
            token.ThrowIfCancellationRequested();
            var result = serialPort.ReadExisting();
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            return serialPort.ReadLine();
        }, token).ConfigureAwait(false);
    }
}
