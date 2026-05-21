using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Axis;

/// <summary>
/// 轴使能模块。
/// </summary>
[Category("轴控制")]
[DisplayName("轴使能")]
[Description("打开指定轴的伺服使能。")]
[MotionModuleIcon("Axis")]
public sealed class ServoOnModule : MotionModuleBase
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
    public int AxisNo
    {
        get => _axisNo;
        set => SetModuleProperty(ref _axisNo, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (AxisNo < 0)
        {
            return ModuleResult.Fail("轴号不能小于 0");
        }

        var ok = await context.GetMotionCard(Param.MotionCardName).ServoOnAsync(AxisNo).ConfigureAwait(false);
        return ok ? ModuleResult.Ok("轴使能完成") : ModuleResult.Fail("轴使能失败");
    }
}
