using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using MotionStudio.App.Infrastructure;
using MotionStudio.App.Services;
using MotionStudio.Core.Engine;
using MotionStudio.Core.Logging;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Core.Project;
using MotionStudio.Core.Variables;
using MotionStudio.Modules.BuiltIn.Axis;
using MotionStudio.Motion.Abstractions;
using MotionStudio.Motion.Cards;

namespace MotionStudio.App.ViewModels;

public enum WorkspacePage
{
    Process,
    SingleAxisDebug
}

/// <summary>
/// 主窗口 ViewModel，负责组织项目、插件、流程引擎、运动卡和页面状态。
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly DialogService _dialogService = new();
    private readonly LogService _logService = new();
    private readonly MotionPluginService _pluginService = new();
    private readonly MotionRuntimeState _runtimeState = new();
    private readonly MotionProcessEngine _processEngine = new();
    private readonly MotionVariableTable _variables = new();
    private readonly Dictionary<string, IMotionCard> _motionCards = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sim-1"] = new SimMotionCard(),
        ["Googol-1"] = new GoogolMotionCard(),
        ["ACS-1"] = new AcsMotionCard()
    };
    private readonly MotionProjectService _projectService;
    private readonly MotionProjectSerializer _serializer;
    private CancellationTokenSource? _runCancellation;
    private MotionProject? _currentProject;
    private string _projectFilePath = string.Empty;
    private WorkspacePage _currentPage = WorkspacePage.Process;

    public MainViewModel()
    {
        _projectService = new MotionProjectService(_pluginService);
        _serializer = new MotionProjectSerializer(_pluginService);

        ModuleBar = new MotionModuleBarViewModel(_pluginService, () => CurrentProject, _runtimeState);
        ProcessBar = new MotionProcessBarViewModel(_projectService, _runtimeState);
        PropertyPanel = new MotionPropertyPanelViewModel(_runtimeState);
        foreach (var cardName in _motionCards.Keys)
        {
            PropertyPanel.MotionCardNames.Add(cardName);
        }

        SingleAxisDebug = new SingleAxisDebugViewModel(_motionCards, _runtimeState, _logService, new MotionConfigService());

        ShowProcessPageCommand = new RelayCommand(_ => CurrentPage = WorkspacePage.Process);
        ShowSingleAxisDebugPageCommand = new RelayCommand(_ => CurrentPage = WorkspacePage.SingleAxisDebug);
        NewProjectCommand = new RelayCommand(_ => NewProject(), _ => !_runtimeState.IsRunning);
        OpenProjectCommand = new AsyncRelayCommand(_ => OpenProjectAsync(), _ => !_runtimeState.IsRunning);
        SaveProjectCommand = new AsyncRelayCommand(_ => SaveProjectAsync(), _ => CurrentProject is not null);
        InitMotionCardCommand = new AsyncRelayCommand(_ => InitMotionCardAsync(), _ => !_runtimeState.IsRunning);
        RunProcessCommand = new AsyncRelayCommand(_ => RunProcessAsync(), _ => CanRunProcess);
        StopProcessCommand = new RelayCommand(_ => StopProcess(), _ => _runtimeState.IsRunning);
        EmergencyStopCommand = new AsyncRelayCommand(_ => EmergencyStopAsync());
        StepSelectedCommand = new AsyncRelayCommand(_ => StepSelectedAsync(), _ => ProcessBar.SelectedModule is not null && !_runtimeState.IsRunning);

        ProcessBar.PropertyChanged += ProcessBarOnPropertyChanged;
        ProcessBar.StepRequested += async (_, module) => await StepModuleAsync(module).ConfigureAwait(true);
        _runtimeState.PropertyChanged += (_, _) => RefreshRuntimeDependentState();
        _processEngine.RuntimeStateChanged += (_, _) => RefreshRuntimeDependentState();

        LoadPlugins();
        NewProject();
        _logService.Info("App", "MotionStudio 已启动，当前注册 Sim-1、Googol-1、ACS-1 三张运动卡。");
    }

    public MotionModuleBarViewModel ModuleBar { get; }

    public MotionProcessBarViewModel ProcessBar { get; }

    public MotionPropertyPanelViewModel PropertyPanel { get; }

    public SingleAxisDebugViewModel SingleAxisDebug { get; }

    public MotionRuntimeState RuntimeState => _runtimeState;

    public ObservableCollection<LogEntry> Logs => _logService.Logs;

    public MotionProject? CurrentProject
    {
        get => _currentProject;
        private set
        {
            if (SetProperty(ref _currentProject, value))
            {
                ProcessBar.CurrentProject = value;
                PropertyPanel.SelectedModule = null;
                ModuleBar.RefreshModules();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ProjectStateText));
                OnPropertyChanged(nameof(CanRunProcess));
            }
        }
    }

    public WorkspacePage CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public string WindowTitle
    {
        get
        {
            var name = CurrentProject?.ProjectName ?? "未打开项目";
            var dirty = CurrentProject?.IsDirty == true ? "*" : string.Empty;
            return $"MotionStudio - {name}{dirty}";
        }
    }

    public string ProjectStateText => CurrentProject is null
        ? "未打开项目"
        : CurrentProject.IsDirty ? "项目已修改" : "项目已保存";

    public string MotionCardStateText => $"{_motionCards.Count(item => item.Value.IsConnected)}/{_motionCards.Count} 张运动卡已连接";

    public bool CanRunProcess => CurrentProject is not null
        && CurrentProject.Modules.Count > 0
        && !_runtimeState.IsRunning
        && _runtimeState.MotionCardConnected;

    public ICommand ShowProcessPageCommand { get; }

    public ICommand ShowSingleAxisDebugPageCommand { get; }

    public ICommand NewProjectCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand InitMotionCardCommand { get; }

    public ICommand RunProcessCommand { get; }

    public ICommand StopProcessCommand { get; }

    public ICommand EmergencyStopCommand { get; }

    public ICommand StepSelectedCommand { get; }

    private void LoadPlugins()
    {
        _pluginService.Clear();
        _pluginService.RegisterAssembly(typeof(ServoOnModule).Assembly);
        _pluginService.InitPlugin(GetPluginDirectory());
        ModuleBar.RefreshModules();
    }

    private void NewProject()
    {
        CurrentProject = _projectService.CreateNewProject("Project1");
        ProjectFilePath = string.Empty;
        SubscribeProject(CurrentProject);
        _logService.Info("Project", "已创建新项目。");
    }

    private async Task OpenProjectAsync()
    {
        var path = _dialogService.ShowOpenProjectDialog();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            LoadPlugins();
            var project = await _serializer.LoadAsync(path).ConfigureAwait(true);
            CurrentProject = project;
            ProjectFilePath = path;
            SubscribeProject(project);
            _logService.Success("Project", $"已打开项目：{Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _logService.Error("Project", $"打开项目失败：{ex.Message}");
        }
    }

    private async Task SaveProjectAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        var path = string.IsNullOrWhiteSpace(ProjectFilePath)
            ? _dialogService.ShowSaveProjectDialog()
            : ProjectFilePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await _serializer.SaveAsync(CurrentProject, path).ConfigureAwait(true);
            ProjectFilePath = path;
            _logService.Success("Project", $"项目已保存：{Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _logService.Error("Project", $"保存项目失败：{ex.Message}");
        }
    }

    private async Task InitMotionCardAsync()
    {
        try
        {
            foreach (var item in _motionCards)
            {
                var ok = await item.Value.InitAsync().ConfigureAwait(true);
                _logService.Write(ok ? LogLevel.Success : LogLevel.Error, "MotionCard", ok ? $"{item.Key} 初始化完成。" : $"{item.Key} 初始化失败。");
            }

            _runtimeState.MotionCardConnected = _motionCards.Values.All(card => card.IsConnected);
        }
        catch (Exception ex)
        {
            _logService.Error("MotionCard", $"初始化异常：{ex.Message}");
        }
        finally
        {
            RefreshRuntimeDependentState();
        }
    }

    private async Task RunProcessAsync()
    {
        if (CurrentProject is null || _runtimeState.IsRunning)
        {
            return;
        }

        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        var context = CreateMotionContext();

        try
        {
            await _processEngine.RunAsync(CurrentProject, context, _runCancellation.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logService.Error("Engine", $"流程执行异常：{ex.Message}");
        }
        finally
        {
            RefreshRuntimeDependentState();
        }
    }

    private void StopProcess()
    {
        if (!_runtimeState.IsRunning)
        {
            return;
        }

        _runtimeState.IsStopRequested = true;
        _runCancellation?.Cancel();
        _logService.Warning("Engine", "已请求停止流程。");
        RefreshRuntimeDependentState();
    }

    private async Task EmergencyStopAsync()
    {
        _runCancellation?.Cancel();
        await _processEngine.EmergencyStopAsync(CreateMotionContext()).ConfigureAwait(true);
        RefreshRuntimeDependentState();
    }

    private async Task StepSelectedAsync()
    {
        if (ProcessBar.SelectedModule is not null)
        {
            await StepModuleAsync(ProcessBar.SelectedModule).ConfigureAwait(true);
        }
    }

    private async Task StepModuleAsync(MotionModuleBase module)
    {
        if (_runtimeState.IsRunning)
        {
            return;
        }

        if (!_motionCards.Values.All(card => card.IsConnected))
        {
            await InitMotionCardAsync().ConfigureAwait(true);
        }

        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        try
        {
            await _processEngine.RunSingleAsync(module, CreateMotionContext(), _runCancellation.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logService.Error("Engine", $"单步执行异常：{ex.Message}");
        }
        finally
        {
            RefreshRuntimeDependentState();
        }
    }

    private MotionContext CreateMotionContext()
    {
        var context = new MotionContext
        {
            Variables = _variables,
            Logger = _logService,
            RuntimeState = _runtimeState
        };

        foreach (var item in _motionCards)
        {
            context.MotionCards[item.Key] = item.Value;
        }

        return context;
    }

    private void ProcessBarOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MotionProcessBarViewModel.SelectedModule))
        {
            PropertyPanel.SelectedModule = ProcessBar.SelectedModule;
            ((AsyncRelayCommand)StepSelectedCommand).RaiseCanExecuteChanged();
        }
    }

    private void SubscribeProject(MotionProject? project)
    {
        if (project is null)
        {
            return;
        }

        project.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MotionProject.IsDirty) or nameof(MotionProject.ProjectName))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ProjectStateText));
            }
        };
    }

    private string ProjectFilePath
    {
        get => _projectFilePath;
        set
        {
            _projectFilePath = value;
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    private void RefreshRuntimeDependentState()
    {
        _runtimeState.MotionCardConnected = _motionCards.Values.All(card => card.IsConnected);
        OnPropertyChanged(nameof(MotionCardStateText));
        OnPropertyChanged(nameof(CanRunProcess));
        ((AsyncRelayCommand)RunProcessCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)OpenProjectCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)InitMotionCardCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopProcessCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StepSelectedCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NewProjectCommand).RaiseCanExecuteChanged();
        ProcessBar.RefreshCommandState();
        SingleAxisDebug.RefreshCommandState();
    }

    private static string GetPluginDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MotionStudio.sln")))
            {
                return Path.Combine(directory.FullName, "plugins");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "plugins");
    }
}
