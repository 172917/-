using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Axis;

/// <summary>
/// 绝对运动模块。
/// </summary>
[Category("坐标运动")]
[DisplayName("绝对运动")]
[Description("驱动指定轴运动到绝对位置。")]
[MotionModuleIcon("Abs")]
public sealed class AbsMoveModule : MotionModuleBase
{
    private string _axisName = "X";
    private int _axisNo;
    private double _targetPosition = 10;
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
    [DisplayName("目标位置")]
    public double TargetPosition
    {
        get => _targetPosition;
        set => SetModuleProperty(ref _targetPosition, value);
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
        var validation = ValidateMotion(AxisNo, VelRatio, Timeout);
        if (!validation.Success)
        {
            return validation;
        }

        var ok = await context.GetMotionCard(Param.MotionCardName)
            .AbsMoveAsync(AxisNo, TargetPosition, VelRatio, Timeout, token)
            .ConfigureAwait(false);
        return ok ? ModuleResult.Ok("绝对运动完成") : ModuleResult.Fail("绝对运动失败");
    }

    private static ModuleResult ValidateMotion(int axisNo, double velRatio, double timeout)
    {
        if (axisNo < 0)
        {
            return ModuleResult.Fail("轴号不能小于 0");
        }

        if (velRatio <= 0 || velRatio > 1)
        {
            return ModuleResult.Fail("速度比例必须在 0 到 1 之间");
        }

        if (timeout <= 0)
        {
            return ModuleResult.Fail("超时必须大于 0");
        }

        return ModuleResult.Ok();
    }
}
