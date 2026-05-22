using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;
using MotionStudio.Modules.BuiltIn.ModuleBinding;

namespace MotionStudio.Modules.BuiltIn.IO;

[Category("IO控制")]
[DisplayName("读取DI")]
[Description("读取指定 DI 状态并写入变量表")]
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
        if (string.IsNullOrWhiteSpace(VariableName))
        {
            return Task.FromResult(ModuleResult.Fail("变量名称不能为空"));
        }

        if (!ModuleBindingResolver.TryResolveInput(context, DiName, Param.MotionCardName, out var input, out var error))
        {
            return Task.FromResult(ModuleResult.Fail(error));
        }

        var value = context.GetMotionCard(input.MotionCardName).GetDI(input.PointName);
        context.Variables.Set(VariableName, value);
        return Task.FromResult(ModuleResult.Ok($"读取 {input.ConfigName}={value}"));
    }
}
