using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Logic;

[Category("逻辑控制")]
[DisplayName("结束如果")]
[Description("条件分支结束标记")]
[MotionModuleIcon("End")]
public sealed class EndIfModule : MotionModuleBase
{
    public EndIfModule()
    {
        ModuleKind = ModuleKind.EndIf;
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        return Task.FromResult(ModuleResult.Ok("EndIf"));
    }
}
