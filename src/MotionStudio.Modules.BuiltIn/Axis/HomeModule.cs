using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Axis;

/// <summary>
/// 轴回零模块。
/// </summary>
[Category("轴控制")]
[DisplayName("回零")]
[Description("驱动指定轴回零。")]
[MotionModuleIcon("Home")]
public sealed class HomeModule : MotionModuleBase
{
    private string _axisName = "X";
    private int _axisNo;
    private double _timeout = 30;

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

    [Category("安全")]
    [DisplayName("超时(s)")]
    public double Timeout
    {
        get => _timeout;
        set => SetModuleProperty(ref _timeout, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (AxisNo < 0)
        {
            return ModuleResult.Fail("轴号不能小于 0");
        }

        if (Timeout <= 0)
        {
            return ModuleResult.Fail("超时必须大于 0");
        }

        var ok = await context.GetMotionCard(Param.MotionCardName).HomeAsync(AxisNo, Timeout).ConfigureAwait(false);
        return ok ? ModuleResult.Ok("回零完成") : ModuleResult.Fail("回零失败");
    }
}
