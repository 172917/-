using System.Collections.ObjectModel;
using MotionStudio.App.Infrastructure;
using MotionStudio.Core.Engine;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Core.Project;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 左侧模块库 ViewModel。
/// </summary>
public sealed class MotionModuleBarViewModel : ObservableObject
{
    private static readonly string[] CategoryOrder =
    [
        "轴控制",
        "坐标运动",
        "IO控制",
        "逻辑控制",
        "变量计算",
        "工艺模块",
        "通讯模块",
        "安全模块"
    ];

    private readonly MotionPluginService _pluginService;
    private readonly Func<MotionProject?> _projectAccessor;
    private readonly MotionRuntimeState _runtimeState;

    public MotionModuleBarViewModel(MotionPluginService pluginService, Func<MotionProject?> projectAccessor, MotionRuntimeState runtimeState)
    {
        _pluginService = pluginService;
        _projectAccessor = projectAccessor;
        _runtimeState = runtimeState;
    }

    public ObservableCollection<ModuleCategoryGroup> ModuleGroups { get; } = new();

    public void RefreshModules()
    {
        ModuleGroups.Clear();
        foreach (var category in CategoryOrder)
        {
            var group = new ModuleCategoryGroup { CategoryName = category };
            foreach (var moduleInfo in _pluginService.PluginDic.Values
                         .Where(m => m.PluginCategory == category)
                         .OrderBy(m => m.PluginName))
            {
                group.Modules.Add(moduleInfo);
            }

            if (group.Modules.Count > 0)
            {
                ModuleGroups.Add(group);
            }
        }
    }

    public bool CanStartDrag(MotionModuleInfo? moduleInfo)
    {
        return moduleInfo is not null && _projectAccessor() is not null && !_runtimeState.IsRunning;
    }
}
