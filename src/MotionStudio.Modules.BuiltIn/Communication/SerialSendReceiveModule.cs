using System.ComponentModel;
using System.IO.Ports;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Communication;

[Category("通讯模块")]
[DisplayName("串口发送接收")]
[Description("通过串口发送文本并接收返回字符串")]
[MotionModuleIcon("COM")]
public sealed class SerialSendReceiveModule : MotionModuleBase
{
    private string _portName = "COM1";
    private int _baudRate = 9600;
    private int _dataBits = 8;
    private Parity _parity = Parity.None;
    private StopBits _stopBits = StopBits.One;
    private string _sendText = "";
    private int _receiveTimeoutMs = 1000;
    private string _resultVariableName = "serialRecv";
    private string _encodingName = "UTF-8";

    [Category("串口参数")]
    [DisplayName("端口名")]
    public string PortName
    {
        get => _portName;
        set => SetModuleProperty(ref _portName, value);
    }

    [Category("串口参数")]
    [DisplayName("波特率")]
    public int BaudRate
    {
        get => _baudRate;
        set => SetModuleProperty(ref _baudRate, value);
    }

    [Category("串口参数")]
    [DisplayName("数据位")]
    public int DataBits
    {
        get => _dataBits;
        set => SetModuleProperty(ref _dataBits, value);
    }

    [Category("串口参数")]
    [DisplayName("校验位")]
    public Parity Parity
    {
        get => _parity;
        set => SetModuleProperty(ref _parity, value);
    }

    [Category("串口参数")]
    [DisplayName("停止位")]
    public StopBits StopBits
    {
        get => _stopBits;
        set => SetModuleProperty(ref _stopBits, value);
    }

    [Category("串口参数")]
    [DisplayName("发送文本")]
    public string SendText
    {
        get => _sendText;
        set => SetModuleProperty(ref _sendText, value);
    }

    [Category("串口参数")]
    [DisplayName("接收超时(ms)")]
    public int ReceiveTimeoutMs
    {
        get => _receiveTimeoutMs;
        set => SetModuleProperty(ref _receiveTimeoutMs, value);
    }

    [Category("串口参数")]
    [DisplayName("结果变量名")]
    public string ResultVariableName
    {
        get => _resultVariableName;
        set => SetModuleProperty(ref _resultVariableName, value);
    }

    [Category("串口参数")]
    [DisplayName("编码")]
    public string EncodingName
    {
        get => _encodingName;
        set => SetModuleProperty(ref _encodingName, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(PortName))
        {
            return ModuleResult.Fail("PortName 不能为空");
        }

        if (BaudRate <= 0 || DataBits <= 0)
        {
            return ModuleResult.Fail("串口参数无效");
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
            var recv = await context.SerialPortService.SendAndReceiveAsync(
                PortName,
                BaudRate,
                DataBits,
                Parity,
                StopBits,
                SendText,
                ReceiveTimeoutMs,
                EncodingName,
                token).ConfigureAwait(false);

            context.Variables.SetVariable(ResultVariableName, recv);
            context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Info, Param.ModuleName, $"串口接收完成，{ResultVariableName} = {recv}");
            return ModuleResult.Ok("串口发送接收完成");
        }
        catch (OperationCanceledException)
        {
            return ModuleResult.Fail("串口发送接收已取消或超时");
        }
        catch (Exception ex)
        {
            return ModuleResult.Fail($"串口发送接收失败: {ex.Message}");
        }
    }
}
