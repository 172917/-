using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Safety;

/// <summary>
/// 轴安全检查模块。
/// </summary>
[Category("安全模块")]
[DisplayName("轴安全检查")]
[Description("检查运动卡连接、急停、轴报警和限位。")]
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
    public int AxisNo
    {
        get => _axisNo;
        set => SetModuleProperty(ref _axisNo, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        var card = context.GetMotionCard(Param.MotionCardName);
        if (!card.IsConnected)
        {
            return Task.FromResult(ModuleResult.Fail($"运动卡 {Param.MotionCardName} 未连接"));
        }

        if (context.RuntimeState.IsEmergencyStop)
        {
            return Task.FromResult(ModuleResult.Fail("急停已触发"));
        }

        if (AxisNo < 0)
        {
            return Task.FromResult(ModuleResult.Fail("轴号不能小于 0"));
        }

        var state = card.GetAxisState(AxisNo);
        if (state.Alarm || state.PositiveLimit || state.NegativeLimit)
        {
            return Task.FromResult(ModuleResult.Fail($"轴号 {AxisNo} 报警或限位"));
        }

        return Task.FromResult(ModuleResult.Ok("轴安全检查通过"));
    }
}
