using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Axis;

/// <summary>
/// 相对运动模块。
/// </summary>
[Category("坐标运动")]
[DisplayName("相对运动")]
[Description("驱动指定轴运动相对距离。")]
[MotionModuleIcon("Rel")]
public sealed class RelMoveModule : MotionModuleBase
{
    private string _axisName = "X";
    private int _axisNo;
    private double _distance = 1;
    private double _velRatio = 0.5;
    private double _timeout = 10;

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

    [Category("运动参数")]
    [DisplayName("相对距离")]
    public double Distance
    {
        get => _distance;
        set => SetModuleProperty(ref _distance, value);
    }

    [Category("运动参数")]
    [DisplayName("速度比例")]
    public double VelRatio
    {
        get => _velRatio;
        set => SetModuleProperty(ref _velRatio, value);
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

        if (VelRatio <= 0 || VelRatio > 1)
        {
            return ModuleResult.Fail("速度比例必须在 0 到 1 之间");
        }

        if (Timeout <= 0)
        {
            return ModuleResult.Fail("超时必须大于 0");
        }

        var ok = await context.GetMotionCard(Param.MotionCardName)
            .RelMoveAsync(AxisNo, Distance, VelRatio, Timeout, token)
            .ConfigureAwait(false);
        return ok ? ModuleResult.Ok("相对运动完成") : ModuleResult.Fail("相对运动失败");
    }
}
