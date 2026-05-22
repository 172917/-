using System.Windows.Input;
using MotionStudio.App.Infrastructure;
using MotionStudio.Core.Engine;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Project;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 流程区 ViewModel。
/// </summary>
public sealed class MotionProcessBarViewModel : ObservableObject
{
    private readonly MotionProjectService _projectService;
    private readonly MotionRuntimeState _runtimeState;
    private readonly Func<bool> _canEditAccessor;
    private MotionProject? _currentProject;
    private MotionModuleBase? _selectedModule;
    private MotionModuleBase? _copiedModule;

    public MotionProcessBarViewModel(MotionProjectService projectService, MotionRuntimeState runtimeState, Func<bool>? canEditAccessor = null)
    {
        _projectService = projectService;
        _runtimeState = runtimeState;
        _canEditAccessor = canEditAccessor ?? (() => true);
        DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => CanEdit && SelectedModule is not null);
        ClearCommand = new RelayCommand(_ => ClearModules(), _ => CanEdit && CurrentProject is not null && CurrentProject.Modules.Count > 0);
        ToggleSelectedCommand = new RelayCommand(_ => ToggleSelected(), _ => CanEdit && SelectedModule is not null);
        CopySelectedCommand = new RelayCommand(_ => CopySelected(), _ => SelectedModule is not null);
        PasteCommand = new RelayCommand(_ => Paste(), _ => CanEdit && _copiedModule is not null && CurrentProject is not null);
        StepSelectedCommand = new RelayCommand(_ => StepRequested?.Invoke(this, SelectedModule!), _ => SelectedModule is not null && !_runtimeState.IsRunning);
        _runtimeState.PropertyChanged += (_, _) => RefreshCommandState();
    }

    public event EventHandler<MotionModuleBase>? StepRequested;

    public MotionProject? CurrentProject
    {
        get => _currentProject;
        set
        {
            if (SetProperty(ref _currentProject, value))
            {
                RefreshCommandState();
            }
        }
    }

    public MotionModuleBase? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                RefreshCommandState();
            }
        }
    }

    public bool CanEdit => CurrentProject is not null && !_runtimeState.IsRunning && _canEditAccessor();

    public ICommand DeleteSelectedCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand ToggleSelectedCommand { get; }

    public ICommand CopySelectedCommand { get; }

    public ICommand PasteCommand { get; }

    public ICommand StepSelectedCommand { get; }

    public void RefreshCommandState()
    {
        OnPropertyChanged(nameof(CanEdit));
        ((RelayCommand)DeleteSelectedCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ToggleSelectedCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CopySelectedCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PasteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StepSelectedCommand).RaiseCanExecuteChanged();
    }

    public MotionModuleBase? AddModule(string pluginName, MotionModuleBase? relativeModule = null)
    {
        if (!CanEdit || CurrentProject is null)
        {
            return null;
        }

        var insertIndex = relativeModule is null ? -1 : CurrentProject.Modules.IndexOf(relativeModule) + 1;
        var module = _projectService.AddModule(CurrentProject, pluginName, insertIndex);
        SelectedModule = module;
        return module;
    }

    public void MoveModule(MotionModuleBase movedModule, MotionModuleBase targetModule)
    {
        if (!CanEdit || CurrentProject is null || movedModule == targetModule)
        {
            return;
        }

        var targetIndex = CurrentProject.Modules.IndexOf(targetModule);
        _projectService.MoveModule(CurrentProject, movedModule, targetIndex);
        SelectedModule = movedModule;
    }

    private void DeleteSelected()
    {
        if (!CanEdit || CurrentProject is null || SelectedModule is null)
        {
            return;
        }

        _projectService.DeleteModule(CurrentProject, SelectedModule);
        SelectedModule = CurrentProject.Modules.FirstOrDefault();
    }

    private void ClearModules()
    {
        if (!CanEdit || CurrentProject is null || CurrentProject.Modules.Count == 0)
        {
            return;
        }

        CurrentProject.Modules.Clear();
        CurrentProject.IsDirty = true;
        SelectedModule = null;
        RefreshCommandState();
    }

    private void ToggleSelected()
    {
        if (!CanEdit || CurrentProject is null || SelectedModule is null)
        {
            return;
        }

        SelectedModule.Param.IsEnabled = !SelectedModule.Param.IsEnabled;
        CurrentProject.IsDirty = true;
    }

    private void CopySelected()
    {
        _copiedModule = SelectedModule;
        RefreshCommandState();
    }

    private void Paste()
    {
        if (!CanEdit || CurrentProject is null || _copiedModule is null)
        {
            return;
        }

        var clone = _projectService.CloneModule(CurrentProject, _copiedModule);
        var insertIndex = SelectedModule is null ? CurrentProject.Modules.Count : CurrentProject.Modules.IndexOf(SelectedModule) + 1;
        CurrentProject.Modules.Insert(insertIndex, clone);
        CurrentProject.IsDirty = true;
        SelectedModule = clone;
    }
}
