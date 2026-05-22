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
using MotionStudio.Core.Services;
using MotionStudio.Core.Security;
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

public sealed class MainViewModel : ObservableObject
{
    private readonly DialogService _dialogService = new();
    private readonly LogService _logService = new();
    private readonly MotionPluginService _pluginService = new();
    private readonly MotionRuntimeState _runtimeState = new();
    private readonly MotionProcessEngine _processEngine = new();
    private readonly MotionVariableTable _variables = new();
    private readonly MotionConfigService _motionConfigService = new();
    private readonly MotionCardFactory _motionCardFactory = new();
    private readonly AuthService _authService = new();
    private readonly Dictionary<string, IMotionCard> _motionCards = new(StringComparer.OrdinalIgnoreCase);

    private readonly MotionProjectService _projectService;
    private readonly ProjectTemplateService _projectTemplateService;
    private readonly MotionProjectSerializer _serializer;

    private CancellationTokenSource? _runCancellation;
    private IReadOnlyList<string> _requiredMotionCards = Array.Empty<string>();
    private MotionProject? _currentProject;
    private string _projectFilePath = string.Empty;
    private WorkspacePage _currentPage = WorkspacePage.Process;

    public MainViewModel()
    {
        _projectService = new MotionProjectService(_pluginService);
        _projectTemplateService = new ProjectTemplateService(_projectService, _pluginService);
        _serializer = new MotionProjectSerializer(_pluginService);

        ModuleBar = new MotionModuleBarViewModel(_pluginService, () => CurrentProject, _runtimeState, () => _authService.HasPermission(Permission.EditFlow));
        ProcessBar = new MotionProcessBarViewModel(_projectService, _runtimeState, () => _authService.HasPermission(Permission.EditFlow));
        PropertyPanel = new MotionPropertyPanelViewModel(_runtimeState, () => _authService.HasPermission(Permission.EditModuleParameters));

        RebuildMotionCardsFromConfig();

        SingleAxisDebug = new SingleAxisDebugViewModel(_motionCards, _runtimeState, _logService, _motionConfigService);

        ShowProcessPageCommand = new RelayCommand(_ => CurrentPage = WorkspacePage.Process);
        ShowSingleAxisDebugPageCommand = new RelayCommand(_ => ShowSingleAxisDebugPage(), _ => CanEnterSingleAxisDebug);
        NewProjectCommand = new RelayCommand(_ => NewProject(), _ => !_runtimeState.IsRunning && _authService.HasPermission(Permission.NewProject));
        NewProjectFromTemplateCommand = new RelayCommand(_ => NewProjectFromTemplate(), _ => !_runtimeState.IsRunning && _authService.HasPermission(Permission.NewProject));
        OpenProjectCommand = new AsyncRelayCommand(_ => OpenProjectAsync(), _ => !_runtimeState.IsRunning && _authService.HasPermission(Permission.OpenProject));
        SaveProjectCommand = new AsyncRelayCommand(_ => SaveProjectAsync(), _ => CurrentProject is not null && _authService.HasPermission(Permission.SaveProject));
        InitMotionCardCommand = new AsyncRelayCommand(_ => InitMotionCardAsync(), _ => !_runtimeState.IsRunning);
        RunProcessCommand = new AsyncRelayCommand(_ => RunProcessAsync(), _ => CanRunProcess && _authService.HasPermission(Permission.StartProcess));
        StopProcessCommand = new RelayCommand(_ => StopProcess(), _ => _runtimeState.IsRunning && _authService.HasPermission(Permission.StopProcess));
        EmergencyStopCommand = new AsyncRelayCommand(_ => EmergencyStopAsync(), _ => _authService.HasPermission(Permission.EmergencyStop));
        StepSelectedCommand = new AsyncRelayCommand(_ => StepSelectedAsync(), _ => ProcessBar.SelectedModule is not null && !_runtimeState.IsRunning);
        LoginCommand = new RelayCommand(_ => Login());
        LogoutCommand = new RelayCommand(_ => Logout(), _ => _authService.CurrentRole != UserRole.Operator);

        ProcessBar.PropertyChanged += ProcessBarOnPropertyChanged;
        ProcessBar.StepRequested += async (_, module) => await StepModuleAsync(module).ConfigureAwait(true);
        _runtimeState.PropertyChanged += (_, _) => RefreshRuntimeDependentState();
        _processEngine.RuntimeStateChanged += (_, _) => RefreshRuntimeDependentState();
        _authService.RoleChanged += (_, _) => RefreshPermissionDependentState();

        LoadPlugins();
        NewProject(skipPermissionCheck: true);
        _logService.Info("App", $"MotionStudio 已启动，当前可用运动卡: {string.Join(" / ", _motionCards.Keys)}。");
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

    public string MotionCardStateText
    {
        get
        {
            if (_requiredMotionCards.Count == 0)
            {
                return "所需运动卡 0/0 已连接";
            }

            var connected = _requiredMotionCards.Count(cardName =>
                _motionCards.TryGetValue(cardName, out var card) && card.IsConnected);
            return $"所需运动卡 {connected}/{_requiredMotionCards.Count} 已连接";
        }
    }

    public bool CanRunProcess => CurrentProject is not null
        && CurrentProject.Modules.Count > 0
        && !_runtimeState.IsRunning
        && _runtimeState.MotionCardConnected;

    public UserRole CurrentRole => _authService.CurrentRole;

    public string CurrentRoleText => $"角色: {CurrentRole}";

    public bool CanEnterSingleAxisDebug => _authService.HasPermission(Permission.EnterSingleAxisDebug);

    public ICommand ShowProcessPageCommand { get; }

    public ICommand ShowSingleAxisDebugPageCommand { get; }

    public ICommand NewProjectCommand { get; }

    public ICommand NewProjectFromTemplateCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand InitMotionCardCommand { get; }

    public ICommand RunProcessCommand { get; }

    public ICommand StopProcessCommand { get; }

    public ICommand EmergencyStopCommand { get; }

    public ICommand StepSelectedCommand { get; }

    public ICommand LoginCommand { get; }

    public ICommand LogoutCommand { get; }

    private void LoadPlugins()
    {
        _pluginService.Clear();
        _pluginService.RegisterAssembly(typeof(ServoOnModule).Assembly);
        _pluginService.InitPlugin(GetPluginDirectory());
        ModuleBar.RefreshModules();
    }

    private void NewProject(bool skipPermissionCheck = false)
    {
        if (!skipPermissionCheck && !EnsurePermission(Permission.NewProject, "Project"))
        {
            return;
        }

        CurrentProject = _projectService.CreateNewProject("Project1");
        ProjectFilePath = string.Empty;
        SubscribeProject(CurrentProject);
        _logService.Info("Project", "已创建新项目。");
    }

    private void NewProjectFromTemplate()
    {
        if (!EnsurePermission(Permission.NewProject, "Project"))
        {
            return;
        }

        var selected = _dialogService.ShowProjectTemplateDialog(_projectTemplateService.GetTemplates());
        if (selected is null)
        {
            return;
        }

        try
        {
            var project = _projectTemplateService.CreateProjectFromTemplate(selected.TemplateName, "Project1");
            CurrentProject = project;
            ProjectFilePath = string.Empty;
            SubscribeProject(project);
            _logService.Success("Project", $"已按模板创建项目: {selected.DisplayName}");
        }
        catch (Exception ex)
        {
            _logService.Error("Project", $"模板创建失败: {ex.Message}");
        }
    }

    private async Task OpenProjectAsync()
    {
        if (!EnsurePermission(Permission.OpenProject, "Project"))
        {
            return;
        }

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
            _logService.Success("Project", $"已打开项目: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _logService.Error("Project", $"打开项目失败: {ex.Message}");
        }
    }

    private async Task SaveProjectAsync()
    {
        if (!EnsurePermission(Permission.SaveProject, "Project"))
        {
            return;
        }

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
            _logService.Success("Project", $"项目已保存: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _logService.Error("Project", $"保存项目失败: {ex.Message}");
        }
    }

    private async Task InitMotionCardAsync()
    {
        try
        {
            if (!SingleAxisDebug.CommitAxisConfigForInitialization())
            {
                _runtimeState.MotionCardConnected = false;
                RefreshRuntimeDependentState();
                return;
            }

            RebuildMotionCardsFromConfig();
            if (!TryGetRequiredMotionCards(out var requiredCards))
            {
                _runtimeState.MotionCardConnected = false;
                RefreshRuntimeDependentState();
                return;
            }

            _requiredMotionCards = requiredCards;
            _logService.Info("MotionCard", $"本次按轴映射需要初始化: {string.Join(" / ", requiredCards)}");

            foreach (var cardName in requiredCards)
            {
                if (!_motionCards.TryGetValue(cardName, out var card))
                {
                    _logService.Error("MotionCard", $"轴映射引用卡 {cardName}，但该卡未注册或未启用。");
                    continue;
                }

                BeginApiTraceScope(card);
                var ok = await card.InitAsync().ConfigureAwait(true);
                var apiTrace = ConsumeApiTrace(card);
                _logService.Write(
                    ok ? LogLevel.Success : LogLevel.Error,
                    "MotionCard",
                    ok ? $"{cardName} 初始化完成。" : $"{cardName} 初始化失败。",
                    apiTrace);
            }
        }
        catch (Exception ex)
        {
            _logService.Error("MotionCard", $"初始化异常: {ex.Message}");
        }
        finally
        {
            RefreshRuntimeDependentState();
            SingleAxisDebug.RefreshStateSnapshot();
        }
    }

    private async Task RunProcessAsync()
    {
        if (!EnsurePermission(Permission.StartProcess, "Engine"))
        {
            return;
        }

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
            _logService.Error("Engine", $"流程执行异常: {ex.Message}");
        }
        finally
        {
            RefreshRuntimeDependentState();
        }
    }

    private void StopProcess()
    {
        if (!EnsurePermission(Permission.StopProcess, "Engine"))
        {
            return;
        }

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
        if (!EnsurePermission(Permission.EmergencyStop, "Engine"))
        {
            return;
        }

        _runCancellation?.Cancel();
        await _processEngine.EmergencyStopAsync(CreateMotionContext()).ConfigureAwait(true);
        RefreshRuntimeDependentState();
    }

    private void ShowSingleAxisDebugPage()
    {
        if (!EnsurePermission(Permission.EnterSingleAxisDebug, "Auth"))
        {
            return;
        }

        CurrentPage = WorkspacePage.SingleAxisDebug;
    }

    private void Login()
    {
        var request = _dialogService.ShowRoleLoginDialog(_authService.CurrentRole);
        if (request is null)
        {
            return;
        }

        if (_authService.Login(request.Role, request.Password))
        {
            _logService.Success("Auth", $"已登录为 {request.Role}。");
            RefreshPermissionDependentState();
            return;
        }

        _logService.Warning("Auth", "登录失败，密码错误。");
        _dialogService.ShowPermissionDenied();
    }

    private void Logout()
    {
        _authService.Logout();
        _logService.Info("Auth", "已切换到 Operator。");
        if (CurrentPage == WorkspacePage.SingleAxisDebug && !CanEnterSingleAxisDebug)
        {
            CurrentPage = WorkspacePage.Process;
        }

        RefreshPermissionDependentState();
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
            _logService.Error("Engine", $"单步执行异常: {ex.Message}");
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
            RuntimeState = _runtimeState,
            MotionConfigService = _motionConfigService
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
        if (_requiredMotionCards.Count == 0)
        {
            _runtimeState.MotionCardConnected = false;
        }
        else
        {
            _runtimeState.MotionCardConnected = _requiredMotionCards.All(cardName =>
                _motionCards.TryGetValue(cardName, out var card) && card.IsConnected);
        }

        OnPropertyChanged(nameof(MotionCardStateText));
        OnPropertyChanged(nameof(CanRunProcess));
        ((AsyncRelayCommand)RunProcessCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)OpenProjectCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)InitMotionCardCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopProcessCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StepSelectedCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NewProjectCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NewProjectFromTemplateCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SaveProjectCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ShowSingleAxisDebugPageCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)EmergencyStopCommand).RaiseCanExecuteChanged();
        ProcessBar.RefreshCommandState();
        SingleAxisDebug.RefreshCommandState();
    }

    private void RefreshPermissionDependentState()
    {
        OnPropertyChanged(nameof(CurrentRole));
        OnPropertyChanged(nameof(CurrentRoleText));
        OnPropertyChanged(nameof(CanEnterSingleAxisDebug));
        RefreshRuntimeDependentState();
        ((RelayCommand)LogoutCommand).RaiseCanExecuteChanged();

        if (CurrentPage == WorkspacePage.SingleAxisDebug && !CanEnterSingleAxisDebug)
        {
            CurrentPage = WorkspacePage.Process;
        }
    }

    private bool EnsurePermission(Permission permission, string source)
    {
        if (_authService.HasPermission(permission))
        {
            return true;
        }

        _logService.Warning(source, "当前权限不足");
        _dialogService.ShowPermissionDenied();
        return false;
    }

    private void RebuildMotionCardsFromConfig()
    {
        var motionData = _motionConfigService.LoadOrCreate();
        _requiredMotionCards = motionData.Axes
            .Select(axis => axis.MotionCardName?.Trim() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var buildResult = _motionCardFactory.Build(motionData.MotionCards);

        _motionCards.Clear();
        foreach (var item in buildResult.MotionCards)
        {
            _motionCards[item.Key] = item.Value;
        }

        var registeredCardNames = _motionCards.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SyncMotionCardNameCollection(PropertyPanel.MotionCardNames, registeredCardNames);
        if (SingleAxisDebug is not null)
        {
            var axisReferencedCardNames = SingleAxisDebug.Axes
                .Select(axis => axis.MotionCardName?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name));
            var singleAxisCardNames = registeredCardNames
                .Concat(axisReferencedCardNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SyncMotionCardNameCollection(SingleAxisDebug.MotionCardNames, singleAxisCardNames);
        }

        foreach (var message in buildResult.Diagnostics)
        {
            _logService.Info("MotionCard", message);
        }
    }

    private bool TryGetRequiredMotionCards(out IReadOnlyList<string> requiredCards)
    {
        var motionData = _motionConfigService.LoadOrCreate();
        var errors = new List<string>();
        var axisNosByCard = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < motionData.Axes.Count; i++)
        {
            var axis = motionData.Axes[i];
            var axisLabel = string.IsNullOrWhiteSpace(axis.AxisName) ? $"Index{i}" : axis.AxisName;

            if (string.IsNullOrWhiteSpace(axis.AxisName))
            {
                errors.Add($"轴配置[{i}] 轴名称为空。");
            }

            if (string.IsNullOrWhiteSpace(axis.MotionCardName))
            {
                errors.Add($"轴 {axisLabel} 未配置 MotionCardName。");
                continue;
            }

            if (!axisNosByCard.TryGetValue(axis.MotionCardName, out var axisNos))
            {
                axisNos = new HashSet<int>();
                axisNosByCard[axis.MotionCardName] = axisNos;
            }

            if (!axisNos.Add(axis.AxisNo))
            {
                errors.Add($"同一运动卡下轴号重复: {axis.MotionCardName} / AxisNo={axis.AxisNo}。");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                _logService.Error("MotionCard", error);
            }

            _logService.Error("MotionCard", "轴映射校验失败，已阻止初始化。");
            requiredCards = Array.Empty<string>();
            return false;
        }

        requiredCards = axisNosByCard.Keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return true;
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

    private static void SyncMotionCardNameCollection(ObservableCollection<string> target, IReadOnlyList<string> desired)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            var current = target[i];
            if (!desired.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                target.RemoveAt(i);
            }
        }

        foreach (var name in desired)
        {
            if (!target.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(name);
            }
        }

        for (var desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
        {
            var name = desired[desiredIndex];
            var currentIndex = target
                .Select((value, index) => new { value, index })
                .First(item => string.Equals(item.value, name, StringComparison.OrdinalIgnoreCase))
                .index;
            if (currentIndex != desiredIndex)
            {
                target.Move(currentIndex, desiredIndex);
            }
        }
    }
}
