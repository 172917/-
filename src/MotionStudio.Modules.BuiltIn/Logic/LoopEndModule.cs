using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Logic;

[Category("逻辑控制")]
[DisplayName("循环结束")]
[Description("循环控制结束")]
[MotionModuleIcon("LoopEnd")]
public sealed class LoopEndModule : MotionModuleBase
{
    public LoopEndModule()
    {
        ModuleKind = ModuleKind.LoopEnd;
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        return Task.FromResult(ModuleResult.Ok("循环结束"));
    }
}
