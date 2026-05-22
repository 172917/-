using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.Safety;

[Category("安全模块")]
[DisplayName("报警复位")]
[Description("清除指定轴或全部轴报警，可选位置清零")]
[MotionModuleIcon("ALM")]
public sealed class ClearAlarmModule : MotionModuleBase
{
    private bool _clearAll;
    private int _axisNo;
    private string _axisName = "X";
    private bool _resetController;

    [Category("复位参数")]
    [DisplayName("全部复位")]
    public bool ClearAll
    {
        get => _clearAll;
        set => SetModuleProperty(ref _clearAll, value);
    }

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

    [Category("复位参数")]
    [DisplayName("位置清零")]
    public bool ResetController
    {
            get => _resetController;
            set => SetModuleProperty(ref _resetController, value);
    }

    public override bool BypassAxisFaultPreCheck => true;

    public override async Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (ResetController)
        {
            if (!ModuleBindingResolver.TryResolveAxis(context, AxisName, AxisNo, Param.MotionCardName, out var resetAxis, out var resetAxisError))
            {
                return ModuleResult.Fail($"位置清零失败: {resetAxisError}");
            }

            var resetCard = context.GetMotionCard(resetAxis.MotionCardName);
            var resetOk = await resetCard.ZeroPositionAsync(resetAxis.AxisNo, token).ConfigureAwait(false);
            if (!resetOk)
            {
                return ModuleResult.Fail($"位置清零失败: {resetAxis.MotionCardName}/{resetAxis.AxisNo}");
            }

            context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Warning, Param.ModuleName, $"位置清零完成: {resetAxis.MotionCardName}/{resetAxis.AxisNo}");
        }

        if (ClearAll)
        {
            var cardName = ResolveMotionCardNameForReset(context);
            var card = context.GetMotionCard(cardName);
            var ok = await card.ClearAllAlarmAsync(token).ConfigureAwait(false);
            if (!ok)
            {
                return ModuleResult.Fail($"全部报警复位失败: {cardName}");
            }

            context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Success, Param.ModuleName, $"全部报警复位完成: {cardName}");
            return ModuleResult.Ok("全部报警复位完成");
        }

        if (!ModuleBindingResolver.TryResolveAxis(context, AxisName, AxisNo, Param.MotionCardName, out var axis, out var error))
        {
            return ModuleResult.Fail(error);
        }

        var axisCard = context.GetMotionCard(axis.MotionCardName);
        var axisOk = await axisCard.ClearAlarmAsync(axis.AxisNo, token).ConfigureAwait(false);
        if (!axisOk)
        {
            return ModuleResult.Fail($"轴报警复位失败: {axis.MotionCardName}/{axis.AxisNo}");
        }

        context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Success, Param.ModuleName, $"轴报警复位完成: {axis.MotionCardName}/{axis.AxisNo}");
        return ModuleResult.Ok("轴报警复位完成");
    }

    private string ResolveMotionCardNameForReset(MotionContext context)
    {
        if (!string.IsNullOrWhiteSpace(AxisName))
        {
            var axis = context.MotionConfigService.GetAxisByName(AxisName);
            if (axis is not null && !string.IsNullOrWhiteSpace(axis.MotionCardName))
            {
                return axis.MotionCardName;
            }
        }

        return string.IsNullOrWhiteSpace(Param.MotionCardName)
            ? MotionContext.DefaultMotionCardName
            : Param.MotionCardName;
    }
}
