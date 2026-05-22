using System.Globalization;
using System.Runtime.InteropServices;
using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

/// <summary>
/// 固高运动卡最小可用适配器：实现上下使能与Jog链路。
/// </summary>
public sealed class GoogolMotionCard : IMotionCard, IApiTraceProvider
{
    private const short FixedCore = 1;
    private const short OpenMode = 5;
    private const short OpenReserved = 1;
    private const short InitAxis = 1;
    private const short InitAxisCount = 8;
    private const string CoreConfigFile = "gtn_core.cfg";
    private readonly List<string> _apiTraceEntries = new();
    private readonly object _apiTraceLock = new();
    private readonly Dictionary<int, AxisRunMode> _axisRunModes = new();
    private readonly Dictionary<int, AxisTelemetryCache> _axisTelemetry = new();
    private readonly object _telemetryLock = new();
    private string _lastMessage = "Googol card is not initialized.";

    public bool IsConnected { get; private set; }

    public void BeginApiTraceScope()
    {
        lock (_apiTraceLock)
        {
            _apiTraceEntries.Clear();
        }
    }

    public string ConsumeApiTrace()
    {
        lock (_apiTraceLock)
        {
            if (_apiTraceEntries.Count == 0)
            {
                return string.Empty;
            }

            var value = string.Join(" | ", _apiTraceEntries);
            _apiTraceEntries.Clear();
            return value;
        }
    }

    public Task<bool> InitAsync()
    {
        var openExpression = $"GTN_Open({OpenMode},{OpenReserved})";
        if (!TryInvokeApi("Init", openExpression, () => GTN_Open(OpenMode, OpenReserved), out var openCode))
        {
            IsConnected = false;
            return Task.FromResult(false);
        }

        if (openCode != 0)
        {
            IsConnected = false;
            _lastMessage = $"Init failed at {openExpression}, code={openCode}.";
            return Task.FromResult(false);
        }

        var resetExpression = $"GTN_Reset({FixedCore})";
        if (!TryInvokeApi("Init", resetExpression, () => GTN_Reset(FixedCore), out var resetCode))
        {
            return Task.FromResult(false);
        }

        if (resetCode != 0)
        {
            IsConnected = false;
            _lastMessage = $"Init failed at {resetExpression}, code={resetCode}.";
            return Task.FromResult(false);
        }

        var loadExpression = $"GTN_LoadConfig({FixedCore},\"{CoreConfigFile}\")";
        if (!TryInvokeApi("Init", loadExpression, () => GTN_LoadConfig(FixedCore, CoreConfigFile), out var loadCode))
        {
            IsConnected = false;
            return Task.FromResult(false);
        }

        if (loadCode != 0)
        {
            IsConnected = false;
            _lastMessage = $"Init failed at {loadExpression}, code={loadCode}.";
            return Task.FromResult(false);
        }

        var clearExpression = $"GTN_ClrSts({FixedCore},{InitAxis},{InitAxisCount})";
        if (!TryInvokeApi("Init", clearExpression, () => GTN_ClrSts(FixedCore, InitAxis, InitAxisCount), out var clrCode))
        {
            return Task.FromResult(false);
        }

        if (clrCode != 0)
        {
            IsConnected = false;
            _lastMessage = $"Init failed at {clearExpression}, code={clrCode}.";
            return Task.FromResult(false);
        }

        var zeroExpression = $"GTN_ZeroPos({FixedCore},{InitAxis},{InitAxisCount})";
        if (!TryInvokeApi("Init", zeroExpression, () => GTN_ZeroPos(FixedCore, InitAxis, InitAxisCount), out var zeroCode))
        {
            return Task.FromResult(false);
        }

        if (zeroCode != 0)
        {
            IsConnected = false;
            _lastMessage = $"Init failed at {zeroExpression}, code={zeroCode}.";
            return Task.FromResult(false);
        }

        lock (_telemetryLock)
        {
            _axisTelemetry.Clear();
        }

        IsConnected = true;
        _lastMessage = "Googol card initialized: Open -> Reset -> LoadConfig -> ClrSts -> ZeroPos.";
        return Task.FromResult(true);
    }

    public Task<bool> CloseAsync()
    {
        IsConnected = false;
        lock (_telemetryLock)
        {
            _axisTelemetry.Clear();
        }
        _lastMessage = "Googol card closed.";
        return Task.FromResult(true);
    }

    public Task<bool> ServoOnAsync(int axisNo) => Task.FromResult(InvokeAxisCommand(axisNo, GTN_AxisOn, "ServoOn", "GTN_AxisOn"));

    public Task<bool> ServoOffAsync(int axisNo) => Task.FromResult(InvokeAxisCommand(axisNo, GTN_AxisOff, "ServoOff", "GTN_AxisOff"));

    public Task<bool> HomeAsync(int axisNo, double timeout) => Task.FromResult(false);

    public Task<bool> AbsMoveAsync(
        int axisNo,
        double position,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        double timeout,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        if (!TryGetMask(axisNo, out var mask))
        {
            return Task.FromResult(false);
        }

        if (!TryConvertToLong(position, out var targetPos, out var conversionError))
        {
            _lastMessage = conversionError;
            return Task.FromResult(false);
        }

        var trapPrm = new TTrapPrm
        {
            acc = acceleration,
            dec = deceleration,
            velStart = 0d,
            smoothTime = smoothTime
        };

        return Task.FromResult(InvokeTrapMove(axisNo, profile, mask, targetPos, velocity, trapPrm, "AbsMove"));
    }

    public Task<bool> RelMoveAsync(
        int axisNo,
        double distance,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        double timeout,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        if (!TryGetMask(axisNo, out var mask))
        {
            return Task.FromResult(false);
        }

        var currentPos = ReadPlannedPosition(profile, captureTrace: true);
        if (!currentPos.Success)
        {
            return Task.FromResult(false);
        }

        var target = currentPos.Position + distance;
        if (!TryConvertToLong(target, out var targetPos, out var conversionError))
        {
            _lastMessage = conversionError;
            return Task.FromResult(false);
        }

        var trapPrm = new TTrapPrm
        {
            acc = acceleration,
            dec = deceleration,
            velStart = 0d,
            smoothTime = smoothTime
        };

        return Task.FromResult(InvokeTrapMove(axisNo, profile, mask, targetPos, velocity, trapPrm, "RelMove"));
    }

    public Task<bool> JogStartAsync(
        int axisNo,
        int direction,
        double velocity,
        double acceleration,
        double deceleration,
        double smoothTime,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        var jog = new TJogPrm
        {
            acc = acceleration,
            dec = deceleration,
            smooth = smoothTime
        };

        return Task.FromResult(InvokeJogStart(axisNo, profile, direction, velocity, jog));
    }

    public Task<bool> PrepareJogModeAsync(int axisNo)
    {
        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(InvokePrepareJog(profile));
    }

    public Task<bool> PrepareTrapModeAsync(int axisNo)
    {
        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(InvokePrepareTrap(axisNo, profile));
    }

    public Task<long?> GetPlannedPositionAsync(int axisNo, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult<long?>(null);
        }

        var result = ReadPlannedPosition(profile, captureTrace: false);
        if (!result.Success)
        {
            return Task.FromResult<long?>(null);
        }

        if (!TryConvertToLong(result.Position, out var longPosition, out _))
        {
            return Task.FromResult<long?>(null);
        }

        return Task.FromResult<long?>(longPosition);
    }

    public Task<bool> StopAxisAsync(int axisNo, bool emergency = false)
    {
        if (emergency)
        {
            _lastMessage = "Emergency stop is not implemented for Googol adapter in current scope.";
            return Task.FromResult(false);
        }

        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        if (!TryGetMask(axisNo, out var mask))
        {
            return Task.FromResult(false);
        }

        var mode = GetAxisRunMode(axisNo);
        if (mode == AxisRunMode.Trap)
        {
            const long stopOption = 0;
            var stopExpression = $"GTN_Stop({FixedCore},{mask},{stopOption})";
            if (!TryInvokeApi("TrapStop", stopExpression, () => GTN_Stop(FixedCore, mask, stopOption), out var stopCode))
            {
                return Task.FromResult(false);
            }

            if (stopCode != 0)
            {
                _lastMessage = $"TrapStop failed at {stopExpression}, code={stopCode}.";
                return Task.FromResult(false);
            }

            _lastMessage = $"TrapStop success, axis={profile}.";
            return Task.FromResult(true);
        }

        var setVelExpression = $"GTN_SetVel({FixedCore},{profile},0)";
        if (!TryInvokeApi("JogStop", setVelExpression, () => GTN_SetVel(FixedCore, profile, 0d), out var setVelCode))
        {
            return Task.FromResult(false);
        }

        if (setVelCode != 0)
        {
            _lastMessage = $"JogStop failed at {setVelExpression}, code={setVelCode}.";
            return Task.FromResult(false);
        }

        var updateExpression = $"GTN_Update({FixedCore},{mask})";
        if (!TryInvokeApi("JogStop", updateExpression, () => GTN_Update(FixedCore, mask), out var updateCode))
        {
            return Task.FromResult(false);
        }

        if (updateCode != 0)
        {
            _lastMessage = $"JogStop failed at {updateExpression}, code={updateCode}.";
            return Task.FromResult(false);
        }

        _lastMessage = $"JogStop success, axis={profile}.";
        return Task.FromResult(true);
    }

    public Task<bool> StopAllAsync(bool emergency = false) => Task.FromResult(false);

    public Task<bool> ClearAlarmAsync(int axisNo, CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> ClearAllAlarmAsync(CancellationToken token = default) => Task.FromResult(false);

    public Task<bool> ZeroPositionAsync(int axisNo, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!TryGetProfile(axisNo, out var profile))
        {
            return Task.FromResult(false);
        }

        var expression = $"GTN_ZeroPos({FixedCore},{profile},1)";
        if (!TryInvokeApi("ZeroPosition", expression, () => GTN_ZeroPos(FixedCore, profile, 1), out var code))
        {
            return Task.FromResult(false);
        }

        if (code != 0)
        {
            _lastMessage = $"ZeroPosition failed: code={code}, core={FixedCore}, axis={profile}.";
            return Task.FromResult(false);
        }

        _lastMessage = $"ZeroPosition success: core={FixedCore}, axis={profile}.";
        return Task.FromResult(true);
    }

    public Task<bool> ResetControllerAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var expression = $"GTN_ZeroPos({FixedCore},{InitAxis},{InitAxisCount})";
        if (!TryInvokeApi("ResetController", expression, () => GTN_ZeroPos(FixedCore, InitAxis, InitAxisCount), out var code))
        {
            return Task.FromResult(false);
        }

        if (code != 0)
        {
            _lastMessage = $"ResetController failed: code={code}, core={FixedCore}.";
            return Task.FromResult(false);
        }

        _lastMessage = $"ResetController success: core={FixedCore}, count={InitAxisCount}.";
        return Task.FromResult(true);
    }

    public double GetAxisPosition(int axisNo) => GetAxisState(axisNo).Position;

    public AxisState GetAxisState(int axisNo)
    {
        if (!TryGetProfile(axisNo, out var profile))
        {
            return new AxisState { AxisNo = axisNo, Message = _lastMessage };
        }

        var cache = GetOrCreateTelemetryCache(axisNo);
        if (!cache.TelemetryEnabled)
        {
            return new AxisState
            {
                AxisNo = axisNo,
                Position = 0d,
                Velocity = 0d,
                PlannedPosition = 0d,
                PlannedVelocity = 0d,
                PlannedAcceleration = 0d,
                ActualAcceleration = 0d,
                HasRealtimeTelemetry = true,
                Message = _lastMessage
            };
        }

        string? firstFailure = null;

        if (TryReadRealtimeValue(
                "GetPrfPos",
                $"GTN_GetPrfPos({FixedCore},{profile},out value,1,NULL)",
                () =>
                {
                    var code = GTN_GetPrfPos(FixedCore, profile, out var value, 1, IntPtr.Zero);
                    return (code, value);
                },
                out var plannedPos,
                out var plannedPosError))
        {
            cache.PlannedPosition = plannedPos;
            cache.HasPlannedPosition = true;
        }
        else if (firstFailure is null)
        {
            firstFailure = plannedPosError;
        }

        if (TryReadRealtimeValue(
                "GetPrfVel",
                $"GTN_GetPrfVel({FixedCore},{profile},out value,1,NULL)",
                () =>
                {
                    var code = GTN_GetPrfVel(FixedCore, profile, out var value, 1, IntPtr.Zero);
                    return (code, value);
                },
                out var plannedVel,
                out var plannedVelError))
        {
            cache.PlannedVelocity = plannedVel;
            cache.HasPlannedVelocity = true;
        }
        else if (firstFailure is null)
        {
            firstFailure = plannedVelError;
        }

        if (TryReadRealtimeValue(
                "GetPrfAcc",
                $"GTN_GetPrfAcc({FixedCore},{profile},out value,1,NULL)",
                () =>
                {
                    var code = GTN_GetPrfAcc(FixedCore, profile, out var value, 1, IntPtr.Zero);
                    return (code, value);
                },
                out var plannedAcc,
                out var plannedAccError))
        {
            cache.PlannedAcceleration = plannedAcc;
            cache.HasPlannedAcceleration = true;
        }
        else if (firstFailure is null)
        {
            firstFailure = plannedAccError;
        }

        if (TryReadRealtimeValue(
                "GetAxisEncPos",
                $"GTN_GetAxisEncPos({FixedCore},{profile},out value,1,NULL)",
                () =>
                {
                    var code = GTN_GetAxisEncPos(FixedCore, profile, out var value, 1, IntPtr.Zero);
                    return (code, value);
                },
                out var actualPos,
                out var actualPosError))
        {
            cache.ActualPosition = actualPos;
            cache.HasActualPosition = true;
        }
        else if (firstFailure is null)
        {
            firstFailure = actualPosError;
        }

        if (TryReadRealtimeValue(
                "GetAxisEncVel",
                $"GTN_GetAxisEncVel({FixedCore},{profile},out value,1,NULL)",
                () =>
                {
                    var code = GTN_GetAxisEncVel(FixedCore, profile, out var value, 1, IntPtr.Zero);
                    return (code, value);
                },
                out var actualVel,
                out var actualVelError))
        {
            cache.ActualVelocity = actualVel;
            cache.HasActualVelocity = true;
        }
        else if (firstFailure is null)
        {
            firstFailure = actualVelError;
        }

        if (TryReadRealtimeValue(
                "GetAxisEncAcc",
                $"GTN_GetAxisEncAcc({FixedCore},{profile},out value,1,NULL)",
                () =>
                {
                    var code = GTN_GetAxisEncAcc(FixedCore, profile, out var value, 1, IntPtr.Zero);
                    return (code, value);
                },
                out var actualAcc,
                out var actualAccError))
        {
            cache.ActualAcceleration = actualAcc;
            cache.HasActualAcceleration = true;
        }
        else if (firstFailure is null)
        {
            firstFailure = actualAccError;
        }

        return new AxisState
        {
            AxisNo = axisNo,
            Position = cache.HasActualPosition ? cache.ActualPosition : 0d,
            Velocity = cache.HasActualVelocity ? cache.ActualVelocity : 0d,
            PlannedPosition = cache.HasPlannedPosition ? cache.PlannedPosition : 0d,
            PlannedVelocity = cache.HasPlannedVelocity ? cache.PlannedVelocity : 0d,
            PlannedAcceleration = cache.HasPlannedAcceleration ? cache.PlannedAcceleration : 0d,
            ActualAcceleration = cache.HasActualAcceleration ? cache.ActualAcceleration : 0d,
            HasRealtimeTelemetry = true,
            Message = firstFailure ?? _lastMessage
        };
    }

    public bool GetDI(string name) => false;

    public bool GetDO(string name) => false;

    public bool SetDO(string name, bool value) => false;

    private bool InvokeAxisCommand(int axisNo, Func<short, short, short> api, string action, string apiName)
    {
        if (!TryGetProfile(axisNo, out var profile))
        {
            return false;
        }

        var expression = $"{apiName}({FixedCore},{profile})";
        if (!TryInvokeApi(action, expression, () => api(FixedCore, profile), out var code))
        {
            return false;
        }

        if (code == 0)
        {
            _lastMessage = $"{action} success: core={FixedCore}, axis={profile}.";
            return true;
        }

        _lastMessage = $"{action} failed: code={code}, core={FixedCore}, axis={profile}.";
        return false;
    }

    private bool InvokePrepareJog(short profile)
    {
        if (!IsConnected)
        {
            _lastMessage = "Googol card is not connected, initialize first.";
            return false;
        }

        var expression = $"GTN_PrfJog({FixedCore},{profile})";
        if (!TryInvokeApi("PrfJog", expression, () => GTN_PrfJog(FixedCore, profile), out var code))
        {
            return false;
        }

        if (code == 0)
        {
            SetAxisRunMode(profile, AxisRunMode.Jog);
            _lastMessage = $"PrfJog success: core={FixedCore}, axis={profile}.";
            return true;
        }

        _lastMessage = $"PrfJog failed: code={code}, core={FixedCore}, axis={profile}.";
        return false;
    }

    private bool InvokePrepareTrap(int axisNo, short profile)
    {
        if (!IsConnected)
        {
            _lastMessage = "Googol card is not connected, initialize first.";
            return false;
        }

        var expression = $"GTN_PrfTrap({FixedCore},{profile})";
        if (!TryInvokeApi("PrfTrap", expression, () => GTN_PrfTrap(FixedCore, profile), out var code))
        {
            return false;
        }

        if (code == 0)
        {
            SetAxisRunMode(axisNo, AxisRunMode.Trap);
            _lastMessage = $"PrfTrap success: core={FixedCore}, axis={profile}.";
            return true;
        }

        _lastMessage = $"PrfTrap failed: code={code}, core={FixedCore}, axis={profile}.";
        return false;
    }

    private (bool Success, double Position) ReadPlannedPosition(short profile, bool captureTrace)
    {
        var expression = $"GTN_GetPrfPos({FixedCore},{profile},out value,1,NULL)";
        try
        {
            var code = GTN_GetPrfPos(FixedCore, profile, out var currentPos, 1, IntPtr.Zero);
            if (captureTrace)
            {
                AddApiTrace($"{expression} => {code}");
            }

            if (code != 0)
            {
                _lastMessage = $"GetPrfPos failed: code={code}, core={FixedCore}, axis={profile}.";
                return (false, 0d);
            }

            return (true, currentPos);
        }
        catch (DllNotFoundException ex)
        {
            IsConnected = false;
            if (captureTrace)
            {
                AddApiTrace($"{expression} => {ex.GetType().Name}");
            }

            _lastMessage = $"GetPrfPos failed: gts.dll missing. {ex.Message}";
            return (false, 0d);
        }
        catch (EntryPointNotFoundException ex)
        {
            if (captureTrace)
            {
                AddApiTrace($"{expression} => {ex.GetType().Name}");
            }

            _lastMessage = $"GetPrfPos failed: API entry not found. {ex.Message}";
            return (false, 0d);
        }
        catch (BadImageFormatException ex)
        {
            IsConnected = false;
            if (captureTrace)
            {
                AddApiTrace($"{expression} => {ex.GetType().Name}");
            }

            _lastMessage = $"GetPrfPos failed: DLL architecture mismatch (x64 required). {ex.Message}";
            return (false, 0d);
        }
    }

    private bool InvokeTrapMove(
        int axisNo,
        short profile,
        long mask,
        long targetPos,
        double velocity,
        TTrapPrm trapPrm,
        string action)
    {
        if (!InvokePrepareTrap(axisNo, profile))
        {
            return false;
        }

        var setTrapExpression =
            $"GTN_SetTrapPrm({FixedCore},{profile},acc={FormatDouble(trapPrm.acc)},dec={FormatDouble(trapPrm.dec)},velStart={FormatDouble(trapPrm.velStart)},smoothTime={FormatDouble(trapPrm.smoothTime)})";
        if (!TryInvokeApi(action, setTrapExpression, () => GTN_SetTrapPrm(FixedCore, profile, ref trapPrm), out var setTrapCode))
        {
            return false;
        }

        if (setTrapCode != 0)
        {
            _lastMessage = $"{action} failed at GTN_SetTrapPrm, code={setTrapCode}, axis={profile}.";
            return false;
        }

        var setPosExpression = $"GTN_SetPos({FixedCore},{profile},{targetPos})";
        if (!TryInvokeApi(action, setPosExpression, () => GTN_SetPos(FixedCore, profile, targetPos), out var setPosCode))
        {
            return false;
        }

        if (setPosCode != 0)
        {
            _lastMessage = $"{action} failed at GTN_SetPos, code={setPosCode}, axis={profile}, pos={targetPos}.";
            return false;
        }

        var setVelExpression = $"GTN_SetVel({FixedCore},{profile},{FormatDouble(velocity)})";
        if (!TryInvokeApi(action, setVelExpression, () => GTN_SetVel(FixedCore, profile, velocity), out var setVelCode))
        {
            return false;
        }

        if (setVelCode != 0)
        {
            _lastMessage = $"{action} failed at GTN_SetVel, code={setVelCode}, axis={profile}.";
            return false;
        }

        var updateExpression = $"GTN_Update({FixedCore},{mask})";
        if (!TryInvokeApi(action, updateExpression, () => GTN_Update(FixedCore, mask), out var updateCode))
        {
            return false;
        }

        if (updateCode != 0)
        {
            _lastMessage = $"{action} failed at GTN_Update, code={updateCode}, mask={mask}.";
            return false;
        }

        SetAxisRunMode(axisNo, AxisRunMode.Trap);
        EnableAxisTelemetry(axisNo);
        _lastMessage = $"{action} success: axis={profile}, pos={targetPos}, vel={velocity}.";
        return true;
    }

    private bool InvokeJogStart(int axisNo, short profile, int direction, double velocity, TJogPrm jogPrm)
    {
        if (!IsConnected)
        {
            _lastMessage = "Googol card is not connected, initialize first.";
            return false;
        }

        if (!TryGetMask(axisNo, out var mask))
        {
            return false;
        }

        var signedVel = Math.Abs(velocity) * (direction >= 0 ? 1d : -1d);

        var setPrmExpression =
            $"GTN_SetJogPrm({FixedCore},{profile},acc={FormatDouble(jogPrm.acc)},dec={FormatDouble(jogPrm.dec)},smooth={FormatDouble(jogPrm.smooth)})";
        if (!TryInvokeApi("JogStart", setPrmExpression, () => GTN_SetJogPrm(FixedCore, profile, ref jogPrm), out var setPrmCode))
        {
            return false;
        }

        if (setPrmCode != 0)
        {
            _lastMessage = $"JogStart failed at GTN_SetJogPrm, code={setPrmCode}, axis={profile}.";
            return false;
        }

        var setVelExpression = $"GTN_SetVel({FixedCore},{profile},{FormatDouble(signedVel)})";
        if (!TryInvokeApi("JogStart", setVelExpression, () => GTN_SetVel(FixedCore, profile, signedVel), out var setVelCode))
        {
            return false;
        }

        if (setVelCode != 0)
        {
            _lastMessage = $"JogStart failed at GTN_SetVel, code={setVelCode}, axis={profile}, vel={signedVel}.";
            return false;
        }

        var updateExpression = $"GTN_Update({FixedCore},{mask})";
        if (!TryInvokeApi("JogStart", updateExpression, () => GTN_Update(FixedCore, mask), out var updateCode))
        {
            return false;
        }

        if (updateCode != 0)
        {
            _lastMessage = $"JogStart failed at GTN_Update, code={updateCode}, mask={mask}.";
            return false;
        }

        SetAxisRunMode(axisNo, AxisRunMode.Jog);
        EnableAxisTelemetry(axisNo);
        _lastMessage =
            $"JogStart success: axis={profile}, vel={signedVel}, acc={jogPrm.acc}, dec={jogPrm.dec}, smooth={jogPrm.smooth}.";
        return true;
    }

    private bool TryInvokeApi(string action, string expression, Func<short> invoke, out short code)
    {
        code = default;
        try
        {
            code = invoke();
            AddApiTrace($"{expression} => {code}");
            return true;
        }
        catch (DllNotFoundException ex)
        {
            IsConnected = false;
            AddApiTrace($"{expression} => {ex.GetType().Name}");
            _lastMessage = $"{action} failed: gts.dll missing. {ex.Message}";
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            AddApiTrace($"{expression} => {ex.GetType().Name}");
            _lastMessage = $"{action} failed: API entry not found. {ex.Message}";
            return false;
        }
        catch (BadImageFormatException ex)
        {
            IsConnected = false;
            AddApiTrace($"{expression} => {ex.GetType().Name}");
            _lastMessage = $"{action} failed: DLL architecture mismatch (x64 required). {ex.Message}";
            return false;
        }
    }

    private void AddApiTrace(string trace)
    {
        lock (_apiTraceLock)
        {
            _apiTraceEntries.Add(trace);
        }
    }

    private AxisTelemetryCache GetOrCreateTelemetryCache(int axisNo)
    {
        lock (_telemetryLock)
        {
            if (_axisTelemetry.TryGetValue(axisNo, out var cache))
            {
                return cache;
            }

            cache = new AxisTelemetryCache();
            _axisTelemetry[axisNo] = cache;
            return cache;
        }
    }

    private void EnableAxisTelemetry(int axisNo)
    {
        lock (_telemetryLock)
        {
            if (!_axisTelemetry.TryGetValue(axisNo, out var cache))
            {
                cache = new AxisTelemetryCache();
                _axisTelemetry[axisNo] = cache;
            }

            cache.TelemetryEnabled = true;
        }
    }

    private bool TryReadRealtimeValue(
        string action,
        string expression,
        Func<(short Code, double Value)> invoke,
        out double value,
        out string error)
    {
        value = 0d;
        error = string.Empty;
        try
        {
            var (code, readValue) = invoke();
            if (code != 0)
            {
                error = $"{action} failed at {expression}, code={code}.";
                _lastMessage = error;
                return false;
            }

            value = readValue;
            return true;
        }
        catch (DllNotFoundException ex)
        {
            IsConnected = false;
            error = $"{action} failed: gts.dll missing. {ex.Message}";
            _lastMessage = error;
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = $"{action} failed: API entry not found. {ex.Message}";
            _lastMessage = error;
            return false;
        }
        catch (BadImageFormatException ex)
        {
            IsConnected = false;
            error = $"{action} failed: DLL architecture mismatch (x64 required). {ex.Message}";
            _lastMessage = error;
            return false;
        }
    }

    private bool TryGetProfile(int axisNo, out short profile)
    {
        profile = 0;

        if (!IsConnected)
        {
            _lastMessage = "Googol card is not connected, initialize first.";
            return false;
        }

        if (axisNo < 0 || axisNo > short.MaxValue)
        {
            _lastMessage = $"Axis number out of range: {axisNo}.";
            return false;
        }

        profile = (short)axisNo;
        return true;
    }

    private bool TryGetMask(int axisNo, out long mask)
    {
        mask = 0;
        if (axisNo < 0 || axisNo >= 63)
        {
            _lastMessage = $"Axis number out of mask range: {axisNo}.";
            return false;
        }

        mask = 1L << axisNo;
        return true;
    }

    private static bool TryConvertToLong(double value, out long converted, out string error)
    {
        converted = 0;
        error = string.Empty;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            error = "Position is invalid (NaN/Infinity).";
            return false;
        }

        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded > long.MaxValue || rounded < long.MinValue)
        {
            error = $"Position out of long range: {value}.";
            return false;
        }

        converted = (long)rounded;
        return true;
    }

    private void SetAxisRunMode(int axisNo, AxisRunMode mode)
    {
        lock (_apiTraceLock)
        {
            _axisRunModes[axisNo] = mode;
        }
    }

    private AxisRunMode GetAxisRunMode(int axisNo)
    {
        lock (_apiTraceLock)
        {
            return _axisRunModes.TryGetValue(axisNo, out var mode) ? mode : AxisRunMode.Unknown;
        }
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.################", CultureInfo.InvariantCulture);
    }

    private sealed class AxisTelemetryCache
    {
        public bool TelemetryEnabled { get; set; }

        public bool HasPlannedPosition { get; set; }

        public double PlannedPosition { get; set; }

        public bool HasPlannedVelocity { get; set; }

        public double PlannedVelocity { get; set; }

        public bool HasPlannedAcceleration { get; set; }

        public double PlannedAcceleration { get; set; }

        public bool HasActualPosition { get; set; }

        public double ActualPosition { get; set; }

        public bool HasActualVelocity { get; set; }

        public double ActualVelocity { get; set; }

        public bool HasActualAcceleration { get; set; }

        public double ActualAcceleration { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TJogPrm
    {
        public double acc;
        public double dec;
        public double smooth;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TTrapPrm
    {
        public double acc;
        public double dec;
        public double velStart;
        public double smoothTime;
    }

    private enum AxisRunMode
    {
        Unknown = 0,
        Jog = 1,
        Trap = 2
    }

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_AxisOn(short core, short axis);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_AxisOff(short core, short axis);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_PrfJog(short core, short profile);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_PrfTrap(short core, short profile);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_SetJogPrm(short core, short profile, ref TJogPrm pPrm);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_SetTrapPrm(short core, short profile, ref TTrapPrm pPrm);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_SetPos(short core, short profile, long pos);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_SetVel(short core, short profile, double vel);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_Update(short core, long mask);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_Stop(short core, long mask, long option);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_GetPrfPos(short core, short profile, out double pValue, short count, IntPtr pClock);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_GetPrfVel(short core, short profile, out double pValue, short count, IntPtr pClock);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_GetPrfAcc(short core, short profile, out double pValue, short count, IntPtr pClock);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_GetAxisEncPos(short core, short axis, out double pValue, short count, IntPtr pClock);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_GetAxisEncVel(short core, short axis, out double pValue, short count, IntPtr pClock);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_GetAxisEncAcc(short core, short axis, out double pValue, short count, IntPtr pClock);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_Open(short type, short reserved);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_Reset(short core);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern short GTN_LoadConfig(short core, string file);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_ClrSts(short core, short axis, short count);

    [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern short GTN_ZeroPos(short core, short axis, short count);
}
