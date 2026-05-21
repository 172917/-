using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Logic;

/// <summary>
/// 条件分支开始占位模块，v1 暂按顺序执行。
/// </summary>
[Category("逻辑控制")]
[DisplayName("如果")]
[Description("条件分支占位，后续扩展子流程。")]
[MotionModuleIcon("If")]
public sealed class IfModule : MotionModuleBase
{
    private string _expression = "true";

    public IfModule()
    {
        ModuleKind = ModuleKind.If;
    }

    [Category("逻辑参数")]
    [DisplayName("表达式")]
    public string Expression
    {
        get => _expression;
        set => SetModuleProperty(ref _expression, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        return Task.FromResult(ModuleResult.Ok("条件分支占位通过"));
    }
}
