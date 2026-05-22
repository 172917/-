using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.Safety;

[Category("安全模块")]
[DisplayName("轴安全检查")]
[Description("检查运动卡连接、急停、轴报警和限位")]
[MotionModuleIcon("Safe")]
public sealed class CheckAxisSafeModule : MotionModuleBase
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

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (!ModuleBindingResolver.TryResolveAxis(context, AxisName, AxisNo, Param.MotionCardName, out var axis, out var error))
        {
            return Task.FromResult(ModuleResult.Fail(error));
        }

        var card = context.GetMotionCard(axis.MotionCardName);
        if (!card.IsConnected)
        {
            return Task.FromResult(ModuleResult.Fail($"运动卡 {axis.MotionCardName} 未连接"));
        }

        if (context.RuntimeState.IsEmergencyStop)
        {
            return Task.FromResult(ModuleResult.Fail("急停已触发"));
        }

        var state = card.GetAxisState(axis.AxisNo);
        if (state.Alarm || state.PositiveLimit || state.NegativeLimit)
        {
            return Task.FromResult(ModuleResult.Fail($"轴号 {axis.AxisNo} 报警或限位"));
        }

        return Task.FromResult(ModuleResult.Ok("轴安全检查通过"));
    }
}
