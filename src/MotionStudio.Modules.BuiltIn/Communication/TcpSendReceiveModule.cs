using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Communication;

[Category("通讯模块")]
[DisplayName("TCP发送接收")]
[Description("通过 TCP 客户端发送文本并接收返回字符串")]
[MotionModuleIcon("TCP")]
public sealed class TcpSendReceiveModule : MotionModuleBase
{
    private string _host = "127.0.0.1";
    private int _port = 5000;
    private string _sendText = "";
    private int _receiveTimeoutMs = 1000;
    private string _resultVariableName = "recv";
    private string _encodingName = "UTF-8";
    private bool _keepConnection;

    [Category("TCP参数")]
    [DisplayName("主机")]
    public string Host
    {
        get => _host;
        set => SetModuleProperty(ref _host, value);
    }

    [Category("TCP参数")]
    [DisplayName("端口")]
    public int Port
    {
        get => _port;
        set => SetModuleProperty(ref _port, value);
    }

    [Category("TCP参数")]
    [DisplayName("发送文本")]
    public string SendText
    {
        get => _sendText;
        set => SetModuleProperty(ref _sendText, value);
    }

    [Category("TCP参数")]
    [DisplayName("接收超时(ms)")]
    public int ReceiveTimeoutMs
    {
        get => _receiveTimeoutMs;
        set => SetModuleProperty(ref _receiveTimeoutMs, value);
    }

    [Category("TCP参数")]
    [DisplayName("结果变量名")]
    public string ResultVariableName
    {
        get => _resultVariableName;
        set => SetModuleProperty(ref _resultVariableName, value);
    }

    [Category("TCP参数")]
    [DisplayName("编码")]
    public string EncodingName
    {
        get => _encodingName;
        set => SetModuleProperty(ref _encodingName, value);
    }

    [Category("TCP参数")]
    [DisplayName("保持连接")]
    public bool KeepConnection
    {
        get => _keepConnection;
        set => SetModuleProperty(ref _keepConnection, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            return ModuleResult.Fail("Host 不能为空");
        }

        if (Port <= 0 || Port > 65535)
        {
            return ModuleResult.Fail("Port 超出范围");
        }

        if (ReceiveTimeoutMs <= 0)
        {
            return ModuleResult.Fail("ReceiveTimeoutMs 必须大于 0");
        }

        if (string.IsNullOrWhiteSpace(ResultVariableName))
        {
            return ModuleResult.Fail("ResultVariableName 不能为空");
        }

        try
        {
            var recv = await context.TcpClientService
                .SendAndReceiveAsync(Host, Port, SendText, ReceiveTimeoutMs, EncodingName, token)
                .ConfigureAwait(false);

            context.Variables.SetVariable(ResultVariableName, recv);
            context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Info, Param.ModuleName, $"TCP 接收完成，{ResultVariableName} = {recv}");
            return ModuleResult.Ok("TCP发送接收完成");
        }
        catch (OperationCanceledException)
        {
            return ModuleResult.Fail("TCP发送接收已取消或超时");
        }
        catch (Exception ex)
        {
            return ModuleResult.Fail($"TCP发送接收失败: {ex.Message}");
        }
    }
}
