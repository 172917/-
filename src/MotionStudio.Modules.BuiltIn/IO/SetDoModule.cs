using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.IO;

/// <summary>
/// 设置 DO 输出模块。
/// </summary>
[Category("IO控制")]
[DisplayName("设置DO")]
[Description("设置指定 DO 输出状态。")]
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
        if (string.IsNullOrWhiteSpace(DoName))
        {
            return Task.FromResult(ModuleResult.Fail("DO 名称不能为空"));
        }

        var ok = context.GetMotionCard(Param.MotionCardName).SetDO(DoName, Value);
        return Task.FromResult(ok ? ModuleResult.Ok("DO 设置完成") : ModuleResult.Fail("DO 设置失败"));
    }
}
