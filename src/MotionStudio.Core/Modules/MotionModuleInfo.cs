namespace MotionStudio.Core.Modules;

/// <summary>
/// 插件模块元数据。
/// </summary>
public sealed class MotionModuleInfo
{
    public string PluginName { get; set; } = string.Empty;

    public string PluginCategory { get; set; } = string.Empty;

    public Type ModuleType { get; set; } = typeof(MotionModuleBase);

    public Type? ParamViewType { get; set; }
//    这个模块被选中时，要用 MoveAxisParamView 这个界面来编辑参数。

//如果 ParamViewType == null，说明这个模块没有专门的参数界面，系统可能使用通用属性面板

    public string Icon { get; set; } = "Module";

    public string Description { get; set; } = string.Empty;
}
