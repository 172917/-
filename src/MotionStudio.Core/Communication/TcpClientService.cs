using System.Net.Sockets;
using System.Text;

namespace MotionStudio.Core.Communication;

public sealed class TcpClientService : ITcpClientService
{
    public async Task<string> SendAndReceiveAsync(
        string host,
        int port,
        string sendText,
        int receiveTimeoutMs,
        string encodingName,
        CancellationToken token)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        using var client = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        connectCts.CancelAfter(Math.Max(100, receiveTimeoutMs));
        await client.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);

        using var stream = client.GetStream();
        var payload = encoding.GetBytes(sendText ?? string.Empty);
        await stream.WriteAsync(payload, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        var buffer = new byte[4096];
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        readCts.CancelAfter(Math.Max(100, receiveTimeoutMs));
        var read = await stream.ReadAsync(buffer, readCts.Token).ConfigureAwait(false);
        if (read <= 0)
        {
            return string.Empty;
        }

        return encoding.GetString(buffer, 0, read);
    }
}
