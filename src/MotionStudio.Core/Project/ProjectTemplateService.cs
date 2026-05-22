using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Core.Project;

public sealed class ProjectTemplateService
{
    public const string Blank = "blank";
    public const string SingleAxisBasic = "single_axis_basic";
    public const string IoTest = "io_test";
    public const string VariableLogicTest = "variable_logic_test";

    private readonly MotionProjectService _projectService;
    private readonly MotionPluginService _pluginService;

    private static readonly IReadOnlyList<ProjectTemplateInfo> Templates =
    [
        new()
        {
            TemplateName = Blank,
            DisplayName = "空白项目",
            Description = "不包含任何模块",
            Category = "基础"
        },
        new()
        {
            TemplateName = SingleAxisBasic,
            DisplayName = "单轴基础测试项目",
            Description = "报警复位/使能/回零/绝对/相对/停止",
            Category = "运动调试"
        },
        new()
        {
            TemplateName = IoTest,
            DisplayName = "IO测试项目",
            Description = "读取DI/等待DI/设置DO/延时",
            Category = "IO调试"
        },
        new()
        {
            TemplateName = VariableLogicTest,
            DisplayName = "变量逻辑测试项目",
            Description = "设置变量/变量计算/If/EndIf/Loop/EndLoop",
            Category = "逻辑调试"
        }
    ];

    public ProjectTemplateService(MotionProjectService projectService, MotionPluginService pluginService)
    {
        _projectService = projectService;
        _pluginService = pluginService;
    }

    public IReadOnlyList<ProjectTemplateInfo> GetTemplates()
    {
        return Templates;
    }

    public MotionProject CreateProjectFromTemplate(string templateName, string projectName = "Project1")
    {
        var project = _projectService.CreateNewProject(projectName);
        switch (templateName)
        {
            case Blank:
                break;
            case SingleAxisBasic:
                AddByType(project, "ClearAlarmModule", axisName: "X");
                AddByType(project, "ServoOnModule", axisName: "X");
                AddByType(project, "HomeModule", axisName: "X");
                AddByType(project, "AbsMoveModule", axisName: "X");
                AddByType(project, "RelMoveModule", axisName: "X");
                AddByType(project, "StopAxisModule", axisName: "X");
                break;
            case IoTest:
                AddByType(project, "ReadDiModule");
                AddByType(project, "WaitDiModule");
                AddByType(project, "SetDoModule");
                AddByType(project, "DelayModule");
                break;
            case VariableLogicTest:
                AddByType(project, "SetVariableModule");
                AddByType(project, "CalcVariableModule");
                AddByType(project, "IfModule");
                AddByType(project, "EndIfModule");
                AddByType(project, "LoopStartModule");
                AddByType(project, "LoopEndModule");
                break;
            default:
                throw new InvalidOperationException($"未知模板: {templateName}");
        }

        project.IsDirty = true;
        return project;
    }

    private void AddByType(MotionProject project, string moduleTypeName, string? axisName = null)
    {
        var pluginName = _pluginService.PluginDic.Values
            .FirstOrDefault(info => info.ModuleType.Name.Equals(moduleTypeName, StringComparison.Ordinal))
            ?.PluginName
            ?? throw new InvalidOperationException($"模板模块未注册: {moduleTypeName}");

        var module = _projectService.AddModule(project, pluginName);
        if (!string.IsNullOrWhiteSpace(axisName))
        {
            var property = module.GetType().GetProperty("AxisName");
            if (property?.CanWrite == true)
            {
                property.SetValue(module, axisName);
            }
        }
    }
}
