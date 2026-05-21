using MotionStudio.Motion.Abstractions;
using MotionStudio.Motion.Config;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MotionStudio.Motion.Cards;

/// <summary>
/// 固高运动卡适配实现：目前仅接入 GTN_AxisOn/GTN_AxisOff，其他能力仍走仿真实现。
/// </summary>
public sealed class GoogolMotionCard : IMotionCard
{
    private readonly SimMotionCard _inner = new();
    private readonly short _core;
    private string _lastError = string.Empty;

    public GoogolMotionCard()
    {
        _core = LoadGoogolCore();
    }

    public bool IsConnected => _inner.IsConnected;

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GTN_AxisOn")]
    private static extern short GTN_AxisOn(short core, short axis);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GTN_AxisOff")]
    private static extern short GTN_AxisOff(short core, short axis);

    public Task<bool> InitAsync() => _inner.InitAsync();

    public Task<bool> CloseAsync() => _inner.CloseAsync();

    public Task<bool> ServoOnAsync(int axisNo)
    {
        if (!TryConvertAxis(axisNo, out var axis))
        {
            return Task.FromResult(false);
        }

        try
        {
            var result = GTN_AxisOn(_core, axis);
            if (result == 0)
            {
                _lastError = string.Empty;
                return Task.FromResult(true);
            }

            _lastError = $"GTN_AxisOn 调用失败，错误码: {result}";
            return Task.FromResult(false);
        }
        catch (DllNotFoundException ex)
        {
            _lastError = $"未找到 gts.dll：{ex.Message}";
            return Task.FromResult(false);
        }
        catch (EntryPointNotFoundException ex)
        {
            _lastError = $"未找到 GTN_AxisOn 入口：{ex.Message}";
            return Task.FromResult(false);
        }
        catch (BadImageFormatException ex)
        {
            _lastError = $"gts.dll 位数不匹配（需 x64）：{ex.Message}";
            return Task.FromResult(false);
        }
    }

    public Task<bool> ServoOffAsync(int axisNo)
    {
        if (!TryConvertAxis(axisNo, out var axis))
        {
            return Task.FromResult(false);
        }

        try
        {
            var result = GTN_AxisOff(_core, axis);
            if (result == 0)
            {
                _lastError = string.Empty;
                return Task.FromResult(true);
            }

            _lastError = $"GTN_AxisOff 调用失败，错误码: {result}";
            return Task.FromResult(false);
        }
        catch (DllNotFoundException ex)
        {
            _lastError = $"未找到 gts.dll：{ex.Message}";
            return Task.FromResult(false);
        }
        catch (EntryPointNotFoundException ex)
        {
            _lastError = $"未找到 GTN_AxisOff 入口：{ex.Message}";
            return Task.FromResult(false);
        }
        catch (BadImageFormatException ex)
        {
            _lastError = $"gts.dll 位数不匹配（需 x64）：{ex.Message}";
            return Task.FromResult(false);
        }
    }

    public Task<bool> HomeAsync(int axisNo, double timeout) => _inner.HomeAsync(axisNo, timeout);

    public Task<bool> AbsMoveAsync(int axisNo, double position, double velRatio, double timeout, CancellationToken token) =>
        _inner.AbsMoveAsync(axisNo, position, velRatio, timeout, token);

    public Task<bool> RelMoveAsync(int axisNo, double distance, double velRatio, double timeout, CancellationToken token) =>
        _inner.RelMoveAsync(axisNo, distance, velRatio, timeout, token);

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false) => _inner.StopAxisAsync(axisNo, emergency);

    public Task<bool> StopAllAsync(bool emergency = false) => _inner.StopAllAsync(emergency);

    public double GetAxisPosition(int axisNo) => _inner.GetAxisPosition(axisNo);

    public AxisState GetAxisState(int axisNo)
    {
        var state = _inner.GetAxisState(axisNo);
        if (!string.IsNullOrWhiteSpace(_lastError))
        {
            state.Message = _lastError;
        }

        return state;
    }

    public bool GetDI(string name) => _inner.GetDI(name);

    public bool GetDO(string name) => _inner.GetDO(name);

    public bool SetDO(string name, bool value) => _inner.SetDO(name, value);

    private bool TryConvertAxis(int axisNo, out short axis)
    {
        axis = 0;
        if (axisNo < 0 || axisNo > short.MaxValue)
        {
            _lastError = $"轴号超出范围：{axisNo}";
            return false;
        }

        axis = (short)axisNo;
        return true;
    }

    private static short LoadGoogolCore()
    {
        var path = ResolveConfigPath();
        if (!File.Exists(path))
        {
            return 1;
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<MotionData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return data?.GoogolCore ?? (short)1;
        }
        catch
        {
            return 1;
        }
    }

    private static string ResolveConfigPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MotionStudio.sln")))
            {
                return Path.Combine(directory.FullName, "configs", "MotionConfig", "motion-config.json");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "configs", "MotionConfig", "motion-config.json");
    }
}
