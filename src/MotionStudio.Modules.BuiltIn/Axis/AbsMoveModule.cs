using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.Axis;

[Category("坐标运动")]
[DisplayName("绝对运动")]
[Description("驱动指定轴运动到绝对位置")]
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
    [Description("当 AxisName 有效时，运行时自动从配置覆盖")]
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
    [Description("当 AxisName 有效时，<=0 时回退到轴配置默认速度")]
    public double VelRatio
    {
        get => _velRatio;
        set => SetModuleProperty(ref _velRatio, value);
    }

    [Category("安全")]
    [DisplayName("超时(s)")]
    [Description("当 AxisName 有效时，<=0 时回退到轴配置默认超时")]
    public double Timeout
    {
        get => _timeout;
        set => SetModuleProperty(ref _timeout, value);
    }

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (!ModuleBindingResolver.TryResolveAxis(context, AxisName, AxisNo, Param.MotionCardName, out var axis, out var error))
        {
            return ModuleResult.Fail(error);
        }

        var velRatio = VelRatio > 0 ? VelRatio : axis.VelocityRatio;
        var timeout = Timeout > 0 ? Timeout : axis.HomeTimeout;
        var validation = ValidateMotion(velRatio, timeout);
        if (!validation.Success)
        {
            return validation;
        }

        var velocity = axis.AbsVelocity > 0 ? axis.AbsVelocity : velRatio * 100d;
        var acceleration = axis.AbsAcceleration > 0 ? axis.AbsAcceleration : 100d;
        var deceleration = axis.AbsDeceleration > 0 ? axis.AbsDeceleration : 100d;
        const double smoothTime = 25d;

        var ok = await context.GetMotionCard(axis.MotionCardName)
            .AbsMoveAsync(axis.AxisNo, TargetPosition, velocity, acceleration, deceleration, smoothTime, timeout, token)
            .ConfigureAwait(false);
        return ok ? ModuleResult.Ok("绝对运动完成") : ModuleResult.Fail("绝对运动失败");
    }

    private static ModuleResult ValidateMotion(double velRatio, double timeout)
    {
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
