using System.ComponentModel;
using System.Reflection;
using MotionStudio.Core.Modules;

namespace MotionStudio.Core.Plugins;

/// <summary>
/// 运动模块插件加载服务。
/// </summary>
public sealed class MotionPluginService
{
    private readonly Dictionary<string, MotionModuleInfo> _pluginDic = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, MotionModuleInfo> PluginDic => _pluginDic;

    public void Clear()
    {
        _pluginDic.Clear();
    }

    /// <summary>
    /// 注册一个程序集中的运动模块。
    /// </summary>
    public void RegisterAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(MotionModuleBase).IsAssignableFrom(type))
            {
                continue;
            }

            var category = type.GetCustomAttribute<CategoryAttribute>()?.Category ?? "未分类";
            var displayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
            var icon = type.GetCustomAttribute<MotionModuleIconAttribute>()?.Icon ?? displayName[..Math.Min(displayName.Length, 1)];

            _pluginDic[displayName] = new MotionModuleInfo
            {
                PluginName = displayName,
                PluginCategory = category,
                ModuleType = type,
                ParamViewType = FindParamViewType(assembly, type),
                Icon = icon,
                Description = description
            };
        }
    }

    /// <summary>
    /// 扫描 plugins 目录中的 Plugin.*.dll。
    /// </summary>
    public void InitPlugin(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            Directory.CreateDirectory(pluginDirectory);
            return;
        }

        foreach (var dllFile in Directory.GetFiles(pluginDirectory, "Plugin.*.dll", SearchOption.AllDirectories))
        {
            var assemblyName = AssemblyName.GetAssemblyName(dllFile);
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(item => AssemblyName.ReferenceMatchesDefinition(item.GetName(), assemblyName))
                ?? Assembly.LoadFrom(dllFile);
            RegisterAssembly(assembly);
        }
    }

    public MotionModuleBase CreateModule(string pluginName)
    {
        if (!_pluginDic.TryGetValue(pluginName, out var info))
        {
            throw new InvalidOperationException($"未找到模块插件：{pluginName}");
        }

        return (MotionModuleBase)Activator.CreateInstance(info.ModuleType)!;
    }

    private static Type? FindParamViewType(Assembly assembly, Type moduleType)
    {
        var expectedName = moduleType.Name.Replace("Module", "ParamView", StringComparison.OrdinalIgnoreCase);
        return assembly.GetTypes().FirstOrDefault(t => t.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 模块图标标识。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MotionModuleIconAttribute : Attribute
{
    public MotionModuleIconAttribute(string icon)
    {
        Icon = icon;
    }

    public string Icon { get; }
}
