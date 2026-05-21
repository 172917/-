using System.Reflection;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Core.Project;

/// <summary>
/// 流程项目编辑服务。
/// </summary>
public sealed class MotionProjectService
{
    private readonly MotionPluginService _pluginService;

    public MotionProjectService(MotionPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public MotionProject CreateNewProject(string projectName = "Project1")
    {
        return new MotionProject
        {
            ProjectId = Environment.TickCount & int.MaxValue,
            ProjectName = projectName
        };
    }

    public MotionModuleBase AddModule(MotionProject project, string pluginName, int insertIndex = -1)
    {
        var module = _pluginService.CreateModule(pluginName);
        module.Param.PluginName = pluginName;
        module.Param.ModuleId = project.NextModuleId();
        module.Param.ModuleName = GenerateUniqueModuleName(project, pluginName);
        module.Param.IsEnabled = true;
        AttachDirtyTracking(project, module);

        if (insertIndex < 0 || insertIndex > project.Modules.Count)
        {
            project.Modules.Add(module);
        }
        else
        {
            project.Modules.Insert(insertIndex, module);
        }

        project.IsDirty = true;
        return module;
    }

    public void DeleteModule(MotionProject project, MotionModuleBase module)
    {
        project.Modules.Remove(module);
        project.IsDirty = true;
    }

    public void MoveModule(MotionProject project, MotionModuleBase module, int targetIndex)
    {
        var oldIndex = project.Modules.IndexOf(module);
        if (oldIndex < 0 || targetIndex < 0 || targetIndex >= project.Modules.Count || oldIndex == targetIndex)
        {
            return;
        }

        project.Modules.Move(oldIndex, targetIndex);
        project.IsDirty = true;
    }

    public MotionModuleBase CloneModule(MotionProject project, MotionModuleBase source)
    {
        var clone = _pluginService.CreateModule(source.Param.PluginName);
        CopyPublicModuleProperties(source, clone);
        clone.Param.PluginName = source.Param.PluginName;
        clone.Param.ModuleId = project.NextModuleId();
        clone.Param.ModuleName = GenerateUniqueModuleName(project, source.Param.PluginName);
        clone.Param.MotionCardName = source.Param.MotionCardName;
        clone.Param.Remark = source.Param.Remark;
        clone.Param.IsEnabled = source.Param.IsEnabled;
        AttachDirtyTracking(project, clone);
        return clone;
    }

    public string GenerateUniqueModuleName(MotionProject project, string pluginName)
    {
        var index = 0;
        while (project.Modules.Any(m => m.Param.ModuleName.Equals(pluginName + index, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return pluginName + index;
    }

    private static void CopyPublicModuleProperties(MotionModuleBase source, MotionModuleBase target)
    {
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.Name == nameof(MotionModuleBase.Param))
            {
                continue;
            }

            var targetProperty = target.GetType().GetProperty(property.Name);
            if (targetProperty?.CanWrite == true)
            {
                targetProperty.SetValue(target, property.GetValue(source));
            }
        }
    }

    private static void AttachDirtyTracking(MotionProject project, MotionModuleBase module)
    {
        module.ParamChanged += (_, _) => project.IsDirty = true;
        module.Param.PropertyChanged += (_, _) => project.IsDirty = true;
    }
}
