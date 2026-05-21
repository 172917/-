using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using MotionStudio.App.Infrastructure;
using MotionStudio.App.Services;
using MotionStudio.Core.Engine;
using MotionStudio.Motion.Abstractions;
using MotionStudio.Motion.Config;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 单轴调试页面 ViewModel。
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
    private string _operationMessage = "请先初始化运动卡。";
    private CancellationTokenSource? _moveCancellation;

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
        ServoOnCommand = new AsyncRelayCommand(
            _ => ExecuteAxisCommandAsync(
                (card, axis, _) => card.ServoOnAsync(axis.AxisNo),
                "轴使能完成。",
                "轴使能失败。"), 
            _ => CanStartMotionCommand);
        ServoOffCommand = new AsyncRelayCommand(_ => ExecuteAxisCommandAsync((card, axis, _) => card.ServoOffAsync(axis.AxisNo), "轴断使能完成。", "轴断使能失败。"), _ => CanStartMotionCommand);
        HomeCommand = new AsyncRelayCommand(_ => ExecuteAxisCommandAsync((card, axis, _) => card.HomeAsync(axis.AxisNo, axis.HomeTimeout), "回零完成。", "回零失败。"), _ => CanStartMotionCommand);
        AbsMoveCommand = new AsyncRelayCommand(_ => MoveAbsoluteAsync(), _ => CanStartMotionCommand);
        RelMoveCommand = new AsyncRelayCommand(_ => MoveRelativeAsync(GetSelectedRelativeDistance()), _ => CanStartMotionCommand);
        JogNegativeCommand = new AsyncRelayCommand(_ => MoveRelativeAsync(-Math.Abs(GetSelectedRelativeDistance())), _ => CanStartMotionCommand);
        JogPositiveCommand = new AsyncRelayCommand(_ => MoveRelativeAsync(Math.Abs(GetSelectedRelativeDistance())), _ => CanStartMotionCommand);
        StopAxisCommand = new AsyncRelayCommand(_ => StopSelectedAxisAsync(false), _ => CanStopCommand);
        EmergencyStopAxisCommand = new AsyncRelayCommand(_ => StopSelectedAxisAsync(true), _ => CanStopCommand);

        _runtimeState.PropertyChanged += (_, _) => RefreshCommandState();
        LoadAxisConfig();

        _stateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
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
        private set => SetProperty(ref _currentState, value);
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

    public bool HasSelectedAxis => SelectedAxis is not null;

    public string SelectedAxisTitle => SelectedAxis?.AxisName ?? "未选择轴";

    public string ConnectionHint => GetConnectionHint();

    public string OperationMessage
    {
        get => _operationMessage;
        private set => SetProperty(ref _operationMessage, value);
    }

    public ICommand AddAxisCommand { get; }

    public ICommand CopyAxisCommand { get; }

    public ICommand DeleteAxisCommand { get; }

    public ICommand SaveAxisConfigCommand { get; }

    public ICommand ServoOnCommand { get; }

    public ICommand ServoOffCommand { get; }

    public ICommand HomeCommand { get; }

    public ICommand AbsMoveCommand { get; }

    public ICommand RelMoveCommand { get; }

    public ICommand JogNegativeCommand { get; }

    public ICommand JogPositiveCommand { get; }

    public ICommand StopAxisCommand { get; }

    public ICommand EmergencyStopAxisCommand { get; }

    private bool CanStartMotionCommand => !_axisCommandRunning
        && !_runtimeState.IsRunning
        && TryGetSelectedConnectedMotionCard(out _, out _);

    private bool CanStopCommand => !_runtimeState.IsRunning
        && TryGetSelectedConnectedMotionCard(out _, out _);

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
        ((AsyncRelayCommand)JogNegativeCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)JogPositiveCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StopAxisCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)EmergencyStopAxisCommand).RaiseCanExecuteChanged();
    }

    private void LoadAxisConfig()
    {
        _motionData = _configService.LoadOrCreate();
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

        _motionData.Axes = Axes.Select(CloneAxis).ToList();
        _configService.Save(_motionData);
        IsDirty = false;
        OperationMessage = $"轴配置已保存：{_configService.ConfigFilePath}";
        _logService.Success("AxisDebug", "轴配置已保存。");
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
        SelectedAxis = Axes.Count == 0 ? null : Axes[Math.Clamp(oldIndex, 0, Axes.Count - 1)];
        IsDirty = true;
        RefreshAxisState();
    }

    private async Task MoveAbsoluteAsync()
    {
        await ExecuteAxisCommandAsync(
            (card, axis, token) => card.AbsMoveAsync(axis.AxisNo, axis.TargetPosition, axis.VelocityRatio, axis.HomeTimeout, token),
            "绝对移动完成。",
            "绝对移动失败。",
            true).ConfigureAwait(true);
    }

    private async Task MoveRelativeAsync(double distance)
    {
        await ExecuteAxisCommandAsync(
            (card, axis, token) => card.RelMoveAsync(axis.AxisNo, distance, axis.VelocityRatio, axis.HomeTimeout, token),
            "相对移动完成。",
            "相对移动失败。",
            true).ConfigureAwait(true);
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
            var ok = await operation(card, SelectedAxis, commandCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
            OperationMessage = ok ? successMessage : failureMessage;
            if (ok)
            {
                _logService.Success("AxisDebug", FormatAxisLogMessage(SelectedAxis, successMessage));
            }
            else
            {
                _logService.Warning("AxisDebug", FormatAxisLogMessage(SelectedAxis, failureMessage));
            }
        }
        catch (OperationCanceledException)
        {
            OperationMessage = "轴运动已停止。";
            _logService.Warning("AxisDebug", FormatAxisLogMessage(SelectedAxis, "轴运动已停止。"));
        }
        catch (Exception ex)
        {
            OperationMessage = $"轴操作异常：{ex.Message}";
            _logService.Error("AxisDebug", OperationMessage);
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

    private async Task StopSelectedAxisAsync(bool emergency)
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
            var ok = await card.StopAxisAsync(SelectedAxis.AxisNo, emergency).ConfigureAwait(true);
            OperationMessage = ok
                ? emergency ? "轴急停已触发。" : "轴停止已触发。"
                : emergency ? "轴急停失败。" : "轴停止失败。";
            _logService.Write(ok ? MotionStudio.Core.Logging.LogLevel.Warning : MotionStudio.Core.Logging.LogLevel.Error, "AxisDebug", FormatAxisLogMessage(SelectedAxis, OperationMessage));
        }
        catch (Exception ex)
        {
            OperationMessage = $"轴停止异常：{ex.Message}";
            _logService.Error("AxisDebug", OperationMessage);
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
            CurrentState = new AxisState { Message = "未选择轴" };
            OnPropertyChanged(nameof(ConnectionHint));
            return;
        }

        if (!TryGetSelectedMotionCard(out var card, out var message))
        {
            CurrentState = new AxisState { AxisNo = SelectedAxis.AxisNo, AxisName = SelectedAxis.AxisName, Message = message };
            OnPropertyChanged(nameof(ConnectionHint));
            return;
        }

        if (!card.IsConnected)
        {
            CurrentState = new AxisState { AxisNo = SelectedAxis.AxisNo, AxisName = SelectedAxis.AxisName, Message = "请先初始化运动卡" };
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
            message = "请先初始化运动卡。";
            return false;
        }

        return true;
    }

    private bool TryGetSelectedMotionCard(out IMotionCard card, out string message)
    {
        card = default!;
        if (SelectedAxis is null)
        {
            message = "未选择轴。";
            return false;
        }

        if (!_motionCards.TryGetValue(SelectedAxis.MotionCardName, out card!))
        {
            message = $"未找到运动卡：{SelectedAxis.MotionCardName}";
            return false;
        }

        message = "运动卡已连接。";
        return true;
    }

    private string GetConnectionHint()
    {
        if (SelectedAxis is null)
        {
            return "未选择轴。";
        }

        if (!_motionCards.TryGetValue(SelectedAxis.MotionCardName, out var card))
        {
            return $"未找到运动卡：{SelectedAxis.MotionCardName}";
        }

        return card.IsConnected ? $"运动卡 {SelectedAxis.MotionCardName} 已连接。" : $"运动卡 {SelectedAxis.MotionCardName} 未初始化。";
    }

    private void TrackAxis(AxisBaseConfig axis)
    {
        axis.PropertyChanged -= AxisOnPropertyChanged;
        axis.PropertyChanged += AxisOnPropertyChanged;
    }

    private void AxisOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        if (ReferenceEquals(sender, SelectedAxis))
        {
            OnPropertyChanged(nameof(SelectedAxisTitle));
            RefreshAxisState();
            RefreshCommandState();
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
    }

    private int GetNextAxisNo(string motionCardName)
    {
        var sameCardAxes = Axes
            .Where(axis => axis.MotionCardName.Equals(motionCardName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameCardAxes.Count == 0 ? 0 : sameCardAxes.Max(axis => axis.AxisNo) + 1;
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
            HomeTimeout = source.HomeTimeout
        };
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
        OperationMessage = $"同一运动卡内轴号不能重复：{first.MotionCardName} / {first.AxisNo}";
        _logService.Warning("AxisDebug", OperationMessage);
        return false;
    }

    private static string FormatAxisLogMessage(AxisBaseConfig axis, string message)
    {
        return $"{axis.AxisName} [{axis.MotionCardName}:{axis.AxisNo}] {message}";
    }
}
