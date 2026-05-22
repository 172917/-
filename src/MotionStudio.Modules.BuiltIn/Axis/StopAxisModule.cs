using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.Axis;

[Category("轴控制")]
[DisplayName("停止轴")]
[Description("停止指定轴，可选择急停模式")]
[MotionModuleIcon("Stop")]
public sealed class StopAxisModule : MotionModuleBase
{
    private string _axisName = "X";
    private int _axisNo;
    private bool _emergency;

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
    [DisplayName("急停模式")]
    public bool Emergency
    {
            get => _emergency;
            set => SetModuleProperty(ref _emergency, value);
    }

    public override bool BypassAxisFaultPreCheck => true;

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (!ModuleBindingResolver.TryResolveAxis(context, AxisName, AxisNo, Param.MotionCardName, out var axis, out var error))
        {
            return ModuleResult.Fail(error);
        }

        var ok = await context.GetMotionCard(axis.MotionCardName).StopAxisAsync(axis.AxisNo, Emergency).ConfigureAwait(false);
        return ok ? ModuleResult.Ok("轴停止完成") : ModuleResult.Fail("轴停止失败");
    }
}
