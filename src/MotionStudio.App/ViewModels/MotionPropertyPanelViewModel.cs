using System.Collections.ObjectModel;
using MotionStudio.App.Infrastructure;
using MotionStudio.Core.Engine;
using MotionStudio.Core.Modules;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 参数面板 ViewModel。
/// </summary>
public sealed class MotionPropertyPanelViewModel : ObservableObject
{
    private readonly MotionRuntimeState _runtimeState;
    private readonly Func<bool> _canEditAccessor;
    private MotionModuleBase? _selectedModule;

    public MotionPropertyPanelViewModel(MotionRuntimeState runtimeState, Func<bool>? canEditAccessor = null)
    {
        _runtimeState = runtimeState;
        _canEditAccessor = canEditAccessor ?? (() => true);
        _runtimeState.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanEdit));
    }

    public ObservableCollection<string> MotionCardNames { get; } = new();

    public bool CanEdit => !_runtimeState.IsRunning && _canEditAccessor();

    public MotionModuleBase? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                OnPropertyChanged(nameof(SelectedParam));
            }
        }
    }

    public MotionModuleParam? SelectedParam => SelectedModule?.Param;
}
