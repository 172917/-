using System.Collections.ObjectModel;
using MotionStudio.Core.Modules;

namespace MotionStudio.App.ViewModels;

/// <summary>
/// 模块库分类。
/// </summary>
public sealed class ModuleCategoryGroup
{
    public string CategoryName { get; set; } = string.Empty;

    public ObservableCollection<MotionModuleInfo> Modules { get; } = new();

    public string Header => $"{CategoryName} ({Modules.Count})";
}
