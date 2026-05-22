using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.Axis;

[Category("轴控制")]
[DisplayName("回零")]
[Description("驱动指定轴回零")]
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
    [Description("当 AxisName 有效时，运行时自动从配置覆盖")]
    public int AxisNo
    {
        get => _axisNo;
        set => SetModuleProperty(ref _axisNo, value);
    }

    [Category("安全")]
    [DisplayName("超时(s)")]
    [Description("当 AxisName 有效时，<=0 将回退到轴配置超时")]
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

        var timeout = Timeout > 0 ? Timeout : axis.HomeTimeout;
        if (timeout <= 0)
        {
            return ModuleResult.Fail("超时必须大于 0");
        }

        var ok = await context.GetMotionCard(axis.MotionCardName).HomeAsync(axis.AxisNo, timeout).ConfigureAwait(false);
        return ok ? ModuleResult.Ok("回零完成") : ModuleResult.Fail("回零失败");
    }
}
