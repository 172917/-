using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.IO;

[Category("IO控制")]
[DisplayName("等待DI")]
[Description("等待指定 DI 到达目标状态")]
[MotionModuleIcon("DI")]
public sealed class WaitDiModule : MotionModuleBase
{
    private string _diName = "DI0";
    private bool _targetValue = true;
    private double _timeout = 5;

    [Category("IO参数")]
    [DisplayName("DI名称")]
    public string DiName
    {
        get => _diName;
        set => SetModuleProperty(ref _diName, value);
    }

    [Category("IO参数")]
    [DisplayName("目标值")]
    public bool TargetValue
    {
        get => _targetValue;
        set => SetModuleProperty(ref _targetValue, value);
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
        if (Timeout <= 0)
        {
            return ModuleResult.Fail("超时必须大于 0");
        }

        if (!ModuleBindingResolver.TryResolveInput(context, DiName, Param.MotionCardName, out var input, out var error))
        {
            return ModuleResult.Fail(error);
        }

        var deadline = DateTime.UtcNow.AddSeconds(Timeout);
        var card = context.GetMotionCard(input.MotionCardName);
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            if (card.GetDI(input.PointName) == TargetValue)
            {
                return ModuleResult.Ok("DI 等待完成");
            }

            await Task.Delay(20, token).ConfigureAwait(false);
        }

        return ModuleResult.Fail("DI 等待超时");
    }
}
