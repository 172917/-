using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.IO;

[Category("IO控制")]
[DisplayName("设置DO")]
[Description("设置指定 DO 输出状态")]
[MotionModuleIcon("DO")]
public sealed class SetDoModule : MotionModuleBase
{
    private string _doName = "DO0";
    private bool _value = true;

    [Category("IO参数")]
    [DisplayName("DO名称")]
    public string DoName
    {
        get => _doName;
        set => SetModuleProperty(ref _doName, value);
    }

    [Category("IO参数")]
    [DisplayName("输出值")]
    public bool Value
    {
        get => _value;
        set => SetModuleProperty(ref _value, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (!ModuleBindingResolver.TryResolveOutput(context, DoName, Param.MotionCardName, out var output, out var error))
        {
            return Task.FromResult(ModuleResult.Fail(error));
        }

        var ok = context.GetMotionCard(output.MotionCardName).SetDO(output.PointName, Value);
        return Task.FromResult(ok
            ? ModuleResult.Ok($"DO 设置完成: {output.ConfigName} -> {output.PointName}")
            : ModuleResult.Fail($"DO 设置失败: {output.ConfigName}"));
    }
}
