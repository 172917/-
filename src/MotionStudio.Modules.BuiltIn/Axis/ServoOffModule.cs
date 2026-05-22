using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.Axis;

[Category("轴控制")]
[DisplayName("轴断使能")]
[Description("关闭指定轴的伺服使能")]
[MotionModuleIcon("Axis")]
public sealed class ServoOffModule : MotionModuleBase
{
    private string _axisName = "X";
    private int _axisNo;

    [Category("轴参数")]
    [DisplayName("轴名称")]
    public string AxisName
    {
        get => _axisName;
        set => SetModuleProperty(ref _axisName, value);
    }

    [Category("轴参数")]
    [DisplayName("轴号")]
    [Description("当 AxisName 有效时，运行时自动从配置覆盖")]
    public int AxisNo
    {
        get => _axisNo;
        set => SetModuleProperty(ref _axisNo, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (!ModuleBindingResolver.TryResolveAxis(context, AxisName, AxisNo, Param.MotionCardName, out var axis, out var error))
        {
            return ModuleResult.Fail(error);
        }

        var ok = await context.GetMotionCard(axis.MotionCardName).ServoOffAsync(axis.AxisNo).ConfigureAwait(false);
        return ok ? ModuleResult.Ok("轴断使能完成") : ModuleResult.Fail("轴断使能失败");
    }
}
