using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.IO;

/// <summary>
/// 读取 DI 输入模块。
/// </summary>
[Category("IO控制")]
[DisplayName("读取DI")]
[Description("读取指定 DI 状态并写入变量表。")]
[MotionModuleIcon("DI")]
public sealed class ReadDiModule : MotionModuleBase
{
    private string _diName = "DI0";
    private string _variableName = "DI0Value";

    [Category("IO参数")]
    [DisplayName("DI名称")]
    public string DiName
    {
        get => _diName;
        set => SetModuleProperty(ref _diName, value);
    }

    [Category("变量")]
    [DisplayName("变量名称")]
    public string VariableName
    {
        get => _variableName;
        set => SetModuleProperty(ref _variableName, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(DiName))
        {
            return Task.FromResult(ModuleResult.Fail("DI 名称不能为空"));
        }

        if (string.IsNullOrWhiteSpace(VariableName))
        {
            return Task.FromResult(ModuleResult.Fail("变量名称不能为空"));
        }

        var value = context.GetMotionCard(Param.MotionCardName).GetDI(DiName);
        context.Variables.Set(VariableName, value);
        return Task.FromResult(ModuleResult.Ok($"读取 {DiName}={value}"));
    }
}
