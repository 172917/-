using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using MotionStudio.App.Infrastructure;
using MotionStudio.App.Services;
using MotionStudio.Core.Engine;
using MotionStudio.Core.Logging;
using MotionStudio.Core.Services;
using MotionStudio.Motion.Abstractions;
using MotionStudio.Motion.Config;

namespace MotionStudio.App.ViewModels;

public enum AxisDebugOperationMode
{
    None,
    Home,
    AbsoluteMove,
    RelativeMove,
    Jog,
    ClearAlarm,
    ResetController
}

/// <summary>
/// Single axis debug view model.
/// </summary>
public sealed class SingleAxisDebugViewModel : ObservableObject
{
    private readonly IReadOnlyDictionary<string, IMotionCard> _motionCards;
    private readonly MotionRuntimeState _runtimeState;
    private readonly LogService _logService;
    private readonly MotionConfigService _configService;
    private readonly DispatcherTimer _stateTimer;
    private MotionData _motionData = new();
    private AxisBaseConfig? _selectedAxis;
    private AxisState _currentState = new();
    private bool _isDirty;
    private bool _axisCommandRunning;
    private bool _isAxisReindexing;
    private bool _isJogRunning;
    private AxisDebugOperationMode _selectedOperationMode = AxisDebugOperationMode.None;
    private string _operationMessage = "请先初始化运动卡。";
    private CancellationTokenSource? _moveCancellation;
    private double _jogVelocity = 50;
    private double _jogAcceleration = 100;
    private double _jogDeceleration = 100;
    private double _jogSmoothTime;

    public SingleAxisDebugViewModel(
        IReadOnlyDictionary<string, IMotionCard> motionCards,
        MotionRuntimeState runtimeState,
        LogService logService,
        MotionConfigService configService)
    {
        _motionCards = motionCards;
        _runtimeState = runtimeState;
        _logService = logService;
        _configService = configService;

        foreach (var cardName in _motionCards.Keys)
        {
            MotionCardNames.Add(cardName);
        }

        AddAxisCommand = new RelayCommand(_ => AddAxis());
        CopyAxisCommand = new RelayCommand(_ => CopySelectedAxis(), _ => SelectedAxis is not null);
        DeleteAxisCommand = new RelayCommand(_ => DeleteSelectedAxis(), _ => SelectedAxis is not null);
        SaveAxisConfigCommand = new RelayCommand(_ => SaveAxisConfig(), _ => IsDirty);
        SelectOperationCommand = new RelayCommand(mode => SelectOperation(mode));

        ServoOnCommand = new AsyncRelayCommand(_ => ExecuteAxisCommandAsync((card, axis, _) => card.ServoOnAsync(axis.AxisNo), "Servo on done.", "Servo on failed."), _ => CanStartMotionCommand);
        ServoOffCommand = new AsyncRelayCommand(_ => ExecuteAxisCommandAsync((card, axis, _) => card.ServoOffAsync(axis.AxisNo), "Servo off done.", "Servo off failed."), _ => CanStartMotionCommand);
        HomeCommand = new AsyncRelayCommand(_ => HomeMoveAsync(), _ => CanStartMotionCommand);
        AbsMoveCommand = new AsyncRelayCommand(_ => MoveAbsoluteAsync(), _ => CanStartMotionCommand);
        RelMoveCommand = new AsyncRelayCommand(_ => MoveRelativeAsync(GetSelectedRelativeDistance()), _ => CanStartMotionCommand);
        StopAxisCommand = new AsyncRelayCommand(_ => StopSelectedAxisAsync(false), _ => CanStopCommand);
        EmergencyStopAxisCommand = new AsyncRelayCommand(_ => StopSelectedAxisAsync(true), _ => CanStopCommand);
        ClearAlarmCommand = new AsyncRelayCommand(_ => ClearAlarmAsync(), _ => CanStopCommand);
        ClearAllAlarmCommand = new AsyncRelayCommand(_ => ClearAllAlarmAsync(), _ => CanStopCommand);
        ZeroPositionCommand = new AsyncRelayCommand(_ => ZeroPositionAsync(), _ => CanStopCommand);

        StartJogPositiveCommand = new AsyncRelayCommand(_ => StartJogPositiveAsync(), _ => CanStartJogCommand);
        StartJogNegativeCommand = new AsyncRelayCommand(_ => StartJogNegativeAsync(), _ => CanStartJogCommand);
        StopJogCommand = new AsyncRelayCommand(_ => StopJogAsync(), _ => CanStopCommand);

        _runtimeState.PropertyChanged += (_, _) =>
        {
            RaiseStatusSnapshotChanged();
            RefreshAxisState();
            RefreshCommandState();
        };

        LoadAxisConfig();

        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _stateTimer.Tick += (_, _) => RefreshAxisState();
        _stateTimer.Start();
    }

    public ObservableCollection<AxisBaseConfig> Axes { get; } = new();

    public ObservableCollection<string> MotionCardNames { get; } = new();

    public AxisBaseConfig? SelectedAxis
    {
        get => _selectedAxis;
        set
        {
            if (SetProperty(ref _selectedAxis, value))
            {
                OnPropertyChanged(nameof(HasSelectedAxis));
                OnPropertyChanged(nameof(SelectedAxisTitle));
                RefreshAxisState();
                RefreshCommandState();
            }
        }
    }

    public AxisState CurrentState
    {
        get => _currentState;
        private set
        {
            if (SetProperty(ref _currentState, value))
            {
                RaiseStatusSnapshotChanged();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                ((RelayCommand)SaveAxisConfigCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsJogRunning
    {
        get => _isJogRunning;
        private set => SetProperty(ref _isJogRunning, value);
    }

    public double JogVelocity
    {
        get => _jogVelocity;
        set => SetProperty(ref _jogVelocity, value);
    }

    public double JogAcceleration
    {
        get => _jogAcceleration;
        set => SetProperty(ref _jogAcceleration, value);
    }

    public double JogDeceleration
    {
        get => _jogDeceleration;
        set => SetProperty(ref _jogDeceleration, value);
    }

    public double JogSmoothTime
    {
        get => _jogSmoothTime;
        set => SetProperty(ref _jogSmoothTime, value);
    }

    public bool HasSelectedAxis => SelectedAxis is not null;

    public string SelectedAxisTitle => SelectedAxis?.AxisName ?? "No axis selected";

    public string ConnectionHint => GetConnectionHint();

    public string OperationMessage
    {
        get => _operationMessage;
        private set => SetProperty(ref _operationMessage, value);
    }

    public AxisDebugOperationMode SelectedOperationMode
    {
        get => _selectedOperationMode;
        set
        {
            if (SetProperty(ref _selectedOperationMode, value))
            {
                OnPropertyChanged(nameof(IsHomeMode));
                OnPropertyChanged(nameof(IsAbsoluteMoveMode));
                OnPropertyChanged(nameof(IsRelativeMoveMode));
                OnPropertyChanged(nameof(IsJogMode));
                OnPropertyChanged(nameof(IsClearAlarmMode));
                OnPropertyChanged(nameof(IsResetControllerMode));
                OnPropertyChanged(nameof(PlannedPosition));
                OnPropertyChanged(nameof(PlannedVelocity));
                OnPropertyChanged(nameof(PlannedAcceleration));
            }
        }
    }

    public bool IsHomeMode => SelectedOperationMode == AxisDebugOperationMode.Home;
    public bool IsAbsoluteMoveMode => SelectedOperationMode == AxisDebugOperationMode.AbsoluteMove;
    public bool IsRelativeMoveMode => SelectedOperationMode == AxisDebugOperationMode.RelativeMove;
    public bool IsJogMode => SelectedOperationMode == AxisDebugOperationMode.Jog;
    public bool IsClearAlarmMode => SelectedOperationMode == AxisDebugOperationMode.ClearAlarm;
    public bool IsResetControllerMode => SelectedOperationMode == AxisDebugOperationMode.ResetController;

    public bool SelectedAxisServoOn => CurrentState.ServoOn;
    public bool SelectedAxisHomed => CurrentState.Homed;
    public bool SelectedAxisMoving => CurrentState.IsMoving;
    public bool SelectedAxisAlarm => CurrentState.Alarm;
    public bool SelectedAxisPositiveLimit => CurrentState.PositiveLimit;
    public bool SelectedAxisNegativeLimit => CurrentState.NegativeLimit;
    public bool SelectedAxisArrived => !CurrentState.IsMoving && !CurrentState.Alarm;
    public bool SelectedAxisEmergencyStop => _runtimeState.IsEmergencyStop;
    public bool SelectedAxisStopped => !CurrentState.IsMoving;

    public double PlannedPosition => GetPlannedPosition();
    public double ActualPosition => CurrentState.Position;
    public double PlannedVelocity => GetPlannedVelocity();
    public double ActualVelocity => CurrentState.Velocity;
    public double PlannedAcceleration => GetPlannedAcceleration();
    public double ActualAcceleration => ShouldUseRealtimeTelemetryDisplay() ? CurrentState.ActualAcceleration : 0;

    public ICommand AddAxisCommand { get; }
    public ICommand CopyAxisCommand { get; }
    public ICommand DeleteAxisCommand { get; }
    public ICommand SaveAxisConfigCommand { get; }
    public ICommand SelectOperationCommand { get; }
    public ICommand ServoOnCommand { get; }
    public ICommand ServoOffCommand { get; }
    public ICommand HomeCommand { get; }
    public ICommand AbsMoveCommand { get; }
    public ICommand RelMoveCommand { get; }
    public ICommand StopAxisCommand { get; }
    public ICommand EmergencyStopAxisCommand { get; }
    public ICommand ClearAlarmCommand { get; }
    public ICommand ClearAllAlarmCommand { get; }
    public ICommand ZeroPositionCommand { get; }
    public ICommand StartJogPositiveCommand { get; }
    public ICommand StartJogNegativeCommand { get; }
    public ICommand StopJogCommand { get; }

    private bool CanStartMotionCommand => !_axisCommandRunning && !_runtimeState.IsRunning && TryGetSelectedConnectedMotionCard(out _, out _);
    private bool CanStartJogCommand => !_runtimeState.IsRunning && !IsJogRunning && TryGetSelectedConnectedMotionCard(out _, out _);
    private bool CanStopCommand => !_runtimeState.IsRunning && TryGetSelectedConnectedMotionCard(out _, out _);

    public void RefreshCommandState()
    {
        OnPropertyChanged(nameof(ConnectionHint));
        ((RelayCommand)CopyAxisCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteAxisCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveAxisConfigCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ServoOnCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ServoOffCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)HomeCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)AbsMoveCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RelMoveCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StopAxisCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)EmergencyStopAxisCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ClearAlarmCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ClearAllAlarmCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ZeroPositionCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StartJogPositiveCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StartJogNegativeCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StopJogCommand).RaiseCanExecuteChanged();
    }

    public void RefreshStateSnapshot()
    {
        RefreshAxisState();
        RefreshCommandState();
    }

    public bool CommitAxisConfigForInitialization()
    {
        ReindexAxisNosSafely();
        if (!ValidateAxisKeys())
        {
            return false;
        }

        _motionData.Axes = Axes.Select(CloneAxis).ToList();
        _configService.Save(_motionData);
        _configService.Reload();
        IsDirty = false;
        return true;
    }

    public Task<bool> StartJogPositiveAsync() => StartJogAsync(1);

    public Task<bool> StartJogNegativeAsync() => StartJogAsync(-1);

    public Task StopJogAsync() => StopSelectedAxisAsync(false, true);

    private async Task<bool> StartJogAsync(int direction)
    {
        if (IsJogRunning)
        {
            return true;
        }

        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return false;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.JogStartAsync(
                SelectedAxis.AxisNo,
                direction,
                JogVelocity,
                JogAcceleration,
                JogDeceleration,
                JogSmoothTime,
                CancellationToken.None).ConfigureAwait(true);
            var apiTrace = ConsumeApiTrace(card);

            if (ok)
            {
                IsJogRunning = true;
                OperationMessage = direction > 0 ? "Jog+ started." : "Jog- started.";
                _logService.Write(LogLevel.Info, "AxisDebug", FormatAxisLogMessage(SelectedAxis, OperationMessage), apiTrace);
            }
            else
            {
                OperationMessage = direction > 0 ? "Jog+ failed to start." : "Jog- failed to start.";
                _logService.Write(LogLevel.Warning, "AxisDebug", FormatAxisLogMessage(SelectedAxis, OperationMessage), apiTrace);
            }

            return ok;
        }
        catch (Exception ex)
        {
            OperationMessage = $"Jog start exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
            return false;
        }
        finally
        {
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private void SelectOperation(object? mode)
    {
        AxisDebugOperationMode? targetMode = null;
        if (mode is AxisDebugOperationMode operationMode)
        {
            targetMode = operationMode;
        }
        else if (mode is string text && Enum.TryParse<AxisDebugOperationMode>(text, true, out var parsed))
        {
            targetMode = parsed;
        }

        if (targetMode is null)
        {
            return;
        }

        SelectedOperationMode = targetMode.Value;
        if (targetMode == AxisDebugOperationMode.Jog)
        {
            _ = PrepareJogModeAfterSelectionAsync();
            return;
        }

        if (targetMode == AxisDebugOperationMode.AbsoluteMove || targetMode == AxisDebugOperationMode.RelativeMove)
        {
            _ = PrepareTrapModeAfterSelectionAsync();
        }
    }

    private async Task PrepareJogModeAfterSelectionAsync()
    {
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.PrepareJogModeAsync(SelectedAxis.AxisNo).ConfigureAwait(true);
            OperationMessage = ok ? "Jog mode prepared." : "Jog mode prepare failed.";
            var apiTrace = ConsumeApiTrace(card);
            _logService.Write(
                ok ? LogLevel.Success : LogLevel.Warning,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                apiTrace);
        }
        catch (Exception ex)
        {
            OperationMessage = $"Jog mode prepare exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private async Task PrepareTrapModeAfterSelectionAsync()
    {
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.PrepareTrapModeAsync(SelectedAxis.AxisNo).ConfigureAwait(true);
            OperationMessage = ok ? "Trap mode prepared." : "Trap mode prepare failed.";
            var apiTrace = ConsumeApiTrace(card);
            _logService.Write(
                ok ? LogLevel.Success : LogLevel.Warning,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                apiTrace);
        }
        catch (Exception ex)
        {
            OperationMessage = $"Trap mode prepare exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private void LoadAxisConfig()
    {
        _motionData = _configService.LoadOrCreate();
        ReindexAxisNos(_motionData.Axes);
        Axes.Clear();
        foreach (var axis in _motionData.Axes)
        {
            NormalizeAxis(axis);
            TrackAxis(axis);
            Axes.Add(axis);
        }

        SelectedAxis = Axes.FirstOrDefault();
        IsDirty = false;
    }

    private void SaveAxisConfig()
    {
        if (!ValidateAxisKeys())
        {
            return;
        }

        ReindexAxisNos(Axes);
        _motionData.Axes = Axes.Select(CloneAxis).ToList();
        _configService.Save(_motionData);
        _configService.Reload();
        IsDirty = false;
        OperationMessage = $"Axis config saved: {_configService.ConfigFilePath}";
        _logService.Success("AxisDebug", "Axis config saved.");
    }

    private void AddAxis()
    {
        var axis = new AxisBaseConfig
        {
            AxisName = GenerateUniqueAxisName("Axis"),
            MotionCardName = MotionCardNames.FirstOrDefault() ?? "Sim-1"
        };
        axis.AxisNo = GetNextAxisNo(axis.MotionCardName);

        TrackAxis(axis);
        Axes.Add(axis);
        SelectedAxis = axis;
        IsDirty = true;
    }

    private void CopySelectedAxis()
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var axis = CloneAxis(SelectedAxis);
        axis.AxisName = GenerateUniqueAxisName(SelectedAxis.AxisName + "_Copy");
        axis.AxisNo = GetNextAxisNo(axis.MotionCardName);
        TrackAxis(axis);
        Axes.Add(axis);
        SelectedAxis = axis;
        IsDirty = true;
    }

    private void DeleteSelectedAxis()
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var oldIndex = Axes.IndexOf(SelectedAxis);
        SelectedAxis.PropertyChanged -= AxisOnPropertyChanged;
        Axes.Remove(SelectedAxis);
        ReindexAxisNos(Axes);
        SelectedAxis = Axes.Count == 0 ? null : Axes[Math.Clamp(oldIndex, 0, Axes.Count - 1)];
        IsDirty = true;
        RefreshAxisState();
    }

    private async Task MoveAbsoluteAsync()
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var velRatio = ToVelocityRatio(SelectedAxis.AbsVelocity, SelectedAxis.VelocityRatio);
        await ExecuteAxisCommandAsync(
            (card, axis, token) => card.AbsMoveAsync(axis.AxisNo, axis.TargetPosition, axis.AbsVelocity, axis.AbsAcceleration, axis.AbsDeceleration, 25d, axis.HomeTimeout, token),
            $"绝对运动完成（速度={SelectedAxis.AbsVelocity:F3}，加速度={SelectedAxis.AbsAcceleration:F3}，减速度={SelectedAxis.AbsDeceleration:F3}，映射速度比例={velRatio:F3}）。",
            "绝对运动失败。",
            true).ConfigureAwait(true);
    }

    private async Task MoveRelativeAsync(double distance)
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var velRatio = ToVelocityRatio(SelectedAxis.RelVelocity, SelectedAxis.VelocityRatio);
        await ExecuteAxisCommandAsync(
            (card, axis, token) => card.RelMoveAsync(axis.AxisNo, distance, axis.RelVelocity, axis.RelAcceleration, axis.RelDeceleration, 25d, axis.HomeTimeout, token),
            $"相对运动完成（速度={SelectedAxis.RelVelocity:F3}，加速度={SelectedAxis.RelAcceleration:F3}，减速度={SelectedAxis.RelDeceleration:F3}，映射速度比例={velRatio:F3}）。",
            "相对运动失败。",
            true).ConfigureAwait(true);
    }

    private async Task HomeMoveAsync()
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var velRatio = ToVelocityRatio(SelectedAxis.HomeVelocity, SelectedAxis.VelocityRatio);
        await ExecuteAxisCommandAsync(
            (card, axis, _) => card.HomeAsync(axis.AxisNo, axis.HomeTimeout),
            $"回零完成（速度={SelectedAxis.HomeVelocity:F3}，加速度={SelectedAxis.HomeAcceleration:F3}，减速度={SelectedAxis.HomeDeceleration:F3}，映射速度比例={velRatio:F3}）。",
            "回零失败。").ConfigureAwait(true);
    }

    private async Task ExecuteAxisCommandAsync(
        Func<IMotionCard, AxisBaseConfig, CancellationToken, Task<bool>> operation,
        string successMessage,
        string failureMessage,
        bool cancellable = false)
    {
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        CancellationTokenSource? commandCancellation = null;
        if (cancellable)
        {
            _moveCancellation?.Cancel();
            _moveCancellation?.Dispose();
            _moveCancellation = new CancellationTokenSource();
            commandCancellation = _moveCancellation;
        }

        _axisCommandRunning = true;
        RefreshCommandState();
        try
        {
            BeginApiTraceScope(card);
            var ok = await operation(card, SelectedAxis, commandCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
            OperationMessage = ok ? successMessage : failureMessage;
            _logService.Write(
                ok ? LogLevel.Success : LogLevel.Warning,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                ConsumeApiTrace(card));
        }
        catch (OperationCanceledException)
        {
            OperationMessage = "Axis move canceled.";
            _logService.Write(LogLevel.Warning, "AxisDebug", FormatAxisLogMessage(SelectedAxis, OperationMessage), ConsumeApiTrace(card));
        }
        catch (Exception ex)
        {
            OperationMessage = $"Axis command exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            if (commandCancellation is not null && ReferenceEquals(commandCancellation, _moveCancellation))
            {
                commandCancellation.Dispose();
                _moveCancellation = null;
            }

            _axisCommandRunning = false;
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private async Task StopSelectedAxisAsync(bool emergency, bool isJogStop = false)
    {
        _moveCancellation?.Cancel();
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.StopAxisAsync(SelectedAxis.AxisNo, emergency).ConfigureAwait(true);
            if (isJogStop)
            {
                OperationMessage = ok ? "Jog stopped." : "Jog stop failed.";
            }
            else
            {
                OperationMessage = ok
                    ? emergency ? "Axis emergency stop triggered." : "Axis stop triggered."
                    : emergency ? "Axis emergency stop failed." : "Axis stop failed.";
            }

            _logService.Write(
                ok ? LogLevel.Warning : LogLevel.Error,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                ConsumeApiTrace(card));
        }
        catch (Exception ex)
        {
            OperationMessage = isJogStop ? $"Jog stop exception: {ex.Message}" : $"Axis stop exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            IsJogRunning = false;
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private async Task ClearAlarmAsync()
    {
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.ClearAlarmAsync(SelectedAxis.AxisNo).ConfigureAwait(true);
            OperationMessage = ok ? "Axis alarm cleared." : "Axis alarm clear failed.";
            _logService.Write(
                ok ? LogLevel.Success : LogLevel.Warning,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                ConsumeApiTrace(card));
        }
        catch (Exception ex)
        {
            OperationMessage = $"Clear alarm exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private async Task ZeroPositionAsync()
    {
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.ZeroPositionAsync(SelectedAxis.AxisNo).ConfigureAwait(true);
            OperationMessage = ok ? "Axis position zeroed." : "Axis position zero failed.";
            _logService.Write(
                ok ? LogLevel.Warning : LogLevel.Error,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                ConsumeApiTrace(card));
        }
        catch (Exception ex)
        {
            OperationMessage = $"Axis zero exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private async Task ClearAllAlarmAsync()
    {
        if (!TryGetSelectedConnectedMotionCard(out var card, out var message) || SelectedAxis is null)
        {
            OperationMessage = message;
            RefreshCommandState();
            return;
        }

        try
        {
            BeginApiTraceScope(card);
            var ok = await card.ClearAllAlarmAsync().ConfigureAwait(true);
            OperationMessage = ok ? "All alarms cleared." : "All alarms clear failed.";
            _logService.Write(
                ok ? LogLevel.Success : LogLevel.Warning,
                "AxisDebug",
                FormatAxisLogMessage(SelectedAxis, OperationMessage),
                ConsumeApiTrace(card));
        }
        catch (Exception ex)
        {
            OperationMessage = $"Clear all alarms exception: {ex.Message}";
            _logService.Write(LogLevel.Error, "AxisDebug", OperationMessage, ConsumeApiTrace(card));
        }
        finally
        {
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private void RefreshAxisState()
    {
        if (SelectedAxis is null)
        {
            CurrentState = new AxisState { Message = "No axis selected" };
            OnPropertyChanged(nameof(ConnectionHint));
            return;
        }

        if (!TryGetSelectedMotionCard(out var card, out var message))
        {
            CurrentState = new AxisState
            {
                AxisNo = SelectedAxis.AxisNo,
                AxisName = SelectedAxis.AxisName,
                Message = message
            };
            OnPropertyChanged(nameof(ConnectionHint));
            return;
        }

        if (!card.IsConnected)
        {
            CurrentState = new AxisState
            {
                AxisNo = SelectedAxis.AxisNo,
                AxisName = SelectedAxis.AxisName,
                Message = "Please initialize motion card first"
            };
            OnPropertyChanged(nameof(ConnectionHint));
            RefreshCommandState();
            return;
        }

        var state = card.GetAxisState(SelectedAxis.AxisNo);
        state.AxisName = SelectedAxis.AxisName;
        CurrentState = state;
        OnPropertyChanged(nameof(ConnectionHint));
    }

    private bool TryGetSelectedConnectedMotionCard(out IMotionCard card, out string message)
    {
        if (!TryGetSelectedMotionCard(out card, out message))
        {
            return false;
        }

        if (!card.IsConnected)
        {
            message = "Please initialize motion card first.";
            return false;
        }

        return true;
    }

    private bool TryGetSelectedMotionCard(out IMotionCard card, out string message)
    {
        card = default!;
        if (SelectedAxis is null)
        {
            message = "No axis selected.";
            return false;
        }

        if (!_motionCards.TryGetValue(SelectedAxis.MotionCardName, out card!))
        {
            message = $"Motion card not found: {SelectedAxis.MotionCardName}";
            return false;
        }

        message = "Motion card connected.";
        return true;
    }

    private string GetConnectionHint()
    {
        if (SelectedAxis is null)
        {
            return "No axis selected.";
        }

        if (!_motionCards.TryGetValue(SelectedAxis.MotionCardName, out var card))
        {
            return $"Motion card not found: {SelectedAxis.MotionCardName}";
        }

        return card.IsConnected
            ? $"Motion card {SelectedAxis.MotionCardName} connected."
            : $"Motion card {SelectedAxis.MotionCardName} not initialized.";
    }

    private void TrackAxis(AxisBaseConfig axis)
    {
        axis.PropertyChanged -= AxisOnPropertyChanged;
        axis.PropertyChanged += AxisOnPropertyChanged;
    }

    private void AxisOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        if (!_isAxisReindexing &&
            (string.Equals(e.PropertyName, nameof(AxisBaseConfig.MotionCardName), StringComparison.Ordinal) ||
             string.Equals(e.PropertyName, nameof(AxisBaseConfig.AxisNo), StringComparison.Ordinal)))
        {
            ReindexAxisNosSafely();
        }

        if (ReferenceEquals(sender, SelectedAxis))
        {
            OnPropertyChanged(nameof(SelectedAxisTitle));
            RefreshAxisState();
            RefreshCommandState();
        }
    }

    private void ReindexAxisNosSafely()
    {
        if (_isAxisReindexing)
        {
            return;
        }

        try
        {
            _isAxisReindexing = true;
            ReindexAxisNos(Axes);
        }
        finally
        {
            _isAxisReindexing = false;
        }
    }

    private void NormalizeAxis(AxisBaseConfig axis)
    {
        if (string.IsNullOrWhiteSpace(axis.AxisName))
        {
            axis.AxisName = GenerateUniqueAxisName("Axis");
        }

        if (string.IsNullOrWhiteSpace(axis.MotionCardName))
        {
            axis.MotionCardName = MotionCardNames.FirstOrDefault() ?? "Sim-1";
        }

        if (axis.HomeTimeout <= 0)
        {
            axis.HomeTimeout = 30;
        }

        if (axis.VelocityRatio <= 0 || axis.VelocityRatio > 1)
        {
            axis.VelocityRatio = 0.5;
        }

        if (axis.AbsVelocity <= 0) axis.AbsVelocity = 50;
        if (axis.AbsAcceleration <= 0) axis.AbsAcceleration = 100;
        if (axis.AbsDeceleration <= 0) axis.AbsDeceleration = 100;
        if (axis.RelVelocity <= 0) axis.RelVelocity = 50;
        if (axis.RelAcceleration <= 0) axis.RelAcceleration = 100;
        if (axis.RelDeceleration <= 0) axis.RelDeceleration = 100;
        if (axis.HomeVelocity <= 0) axis.HomeVelocity = 30;
        if (axis.HomeAcceleration <= 0) axis.HomeAcceleration = 80;
        if (axis.HomeDeceleration <= 0) axis.HomeDeceleration = 80;
    }

    private int GetNextAxisNo(string motionCardName)
    {
        var sameCardAxes = Axes
            .Where(axis => axis.MotionCardName.Equals(motionCardName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameCardAxes.Count == 0 ? 1 : sameCardAxes.Max(axis => axis.AxisNo) + 1;
    }

    private string GenerateUniqueAxisName(string prefix)
    {
        var baseName = string.IsNullOrWhiteSpace(prefix) ? "Axis" : prefix;
        var name = baseName;
        var index = 1;
        while (Axes.Any(axis => axis.AxisName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            name = baseName + index;
            index++;
        }

        return name;
    }

    private double GetSelectedRelativeDistance()
    {
        return SelectedAxis?.RelativeDistance ?? 0;
    }

    private static AxisBaseConfig CloneAxis(AxisBaseConfig source)
    {
        return new AxisBaseConfig
        {
            AxisName = source.AxisName,
            AxisNo = source.AxisNo,
            MotionCardName = source.MotionCardName,
            VelocityRatio = source.VelocityRatio,
            TargetPosition = source.TargetPosition,
            RelativeDistance = source.RelativeDistance,
            HomeTimeout = source.HomeTimeout,
            AbsVelocity = source.AbsVelocity,
            AbsAcceleration = source.AbsAcceleration,
            AbsDeceleration = source.AbsDeceleration,
            RelVelocity = source.RelVelocity,
            RelAcceleration = source.RelAcceleration,
            RelDeceleration = source.RelDeceleration,
            HomeVelocity = source.HomeVelocity,
            HomeAcceleration = source.HomeAcceleration,
            HomeDeceleration = source.HomeDeceleration
        };
    }

    private static void ReindexAxisNos(IEnumerable<AxisBaseConfig> axes)
    {
        foreach (var group in axes.GroupBy(a => a.MotionCardName, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderBy(a => a.AxisNo)
                .ThenBy(a => a.AxisName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].AxisNo = i + 1;
            }
        }
    }

    private bool ValidateAxisKeys()
    {
        var duplicate = Axes
            .GroupBy(axis => $"{axis.MotionCardName.ToUpperInvariant()}|{axis.AxisNo}")
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is null)
        {
            return true;
        }

        var first = duplicate.First();
        OperationMessage = $"Axis number must be unique in card: {first.MotionCardName} / {first.AxisNo}";
        _logService.Warning("AxisDebug", OperationMessage);
        return false;
    }

    private static string FormatAxisLogMessage(AxisBaseConfig axis, string message)
    {
        return $"{axis.AxisName} [{axis.MotionCardName}:{axis.AxisNo}] {message}";
    }

    private static void BeginApiTraceScope(IMotionCard card)
    {
        if (card is IApiTraceProvider traceProvider)
        {
            traceProvider.BeginApiTraceScope();
        }
    }

    private static string ConsumeApiTrace(IMotionCard card)
    {
        return card is IApiTraceProvider traceProvider
            ? traceProvider.ConsumeApiTrace()
            : string.Empty;
    }

    private void RaiseStatusSnapshotChanged()
    {
        OnPropertyChanged(nameof(SelectedAxisServoOn));
        OnPropertyChanged(nameof(SelectedAxisHomed));
        OnPropertyChanged(nameof(SelectedAxisMoving));
        OnPropertyChanged(nameof(SelectedAxisAlarm));
        OnPropertyChanged(nameof(SelectedAxisPositiveLimit));
        OnPropertyChanged(nameof(SelectedAxisNegativeLimit));
        OnPropertyChanged(nameof(SelectedAxisArrived));
        OnPropertyChanged(nameof(SelectedAxisEmergencyStop));
        OnPropertyChanged(nameof(SelectedAxisStopped));
        OnPropertyChanged(nameof(PlannedPosition));
        OnPropertyChanged(nameof(ActualPosition));
        OnPropertyChanged(nameof(PlannedVelocity));
        OnPropertyChanged(nameof(ActualVelocity));
        OnPropertyChanged(nameof(PlannedAcceleration));
        OnPropertyChanged(nameof(ActualAcceleration));
    }

    private double GetPlannedPosition()
    {
        if (SelectedAxis is null)
        {
            return 0;
        }

        if (ShouldUseRealtimeTelemetryDisplay())
        {
            return CurrentState.PlannedPosition;
        }

        return SelectedOperationMode == AxisDebugOperationMode.RelativeMove
            ? CurrentState.Position + SelectedAxis.RelativeDistance
            : SelectedAxis.TargetPosition;
    }

    private double GetPlannedVelocity()
    {
        if (SelectedAxis is null)
        {
            return 0;
        }

        if (ShouldUseRealtimeTelemetryDisplay())
        {
            return CurrentState.PlannedVelocity;
        }

        return SelectedOperationMode switch
        {
            AxisDebugOperationMode.Home => SelectedAxis.HomeVelocity,
            AxisDebugOperationMode.RelativeMove => SelectedAxis.RelVelocity,
            AxisDebugOperationMode.Jog => JogVelocity,
            _ => SelectedAxis.AbsVelocity
        };
    }

    private double GetPlannedAcceleration()
    {
        if (SelectedAxis is null)
        {
            return 0;
        }

        if (ShouldUseRealtimeTelemetryDisplay())
        {
            return CurrentState.PlannedAcceleration;
        }

        return SelectedOperationMode switch
        {
            AxisDebugOperationMode.Home => SelectedAxis.HomeAcceleration,
            AxisDebugOperationMode.RelativeMove => SelectedAxis.RelAcceleration,
            AxisDebugOperationMode.Jog => JogAcceleration,
            _ => SelectedAxis.AbsAcceleration
        };
    }

    private static double ToVelocityRatio(double velocity, double fallbackRatio)
    {
        if (velocity > 0)
        {
            return Math.Clamp(velocity / 100d, 0.01, 1d);
        }

        return Math.Clamp(fallbackRatio, 0.01, 1d);
    }

    private bool ShouldUseRealtimeTelemetryDisplay()
    {
        return CurrentState.HasRealtimeTelemetry ||
            SelectedAxis?.MotionCardName.StartsWith("Googol", StringComparison.OrdinalIgnoreCase) == true;
    }
}
