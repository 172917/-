using System.ComponentModel;
using MotionStudio.Core.Expressions;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Variables;

[Category("变量计算")]
[DisplayName("设置变量")]
[Description("计算表达式并写入变量")]
[MotionModuleIcon("Var")]
public sealed class SetVariableModule : MotionModuleBase
{
    private string _variableName = "var1";
    private string _valueExpression = "0";

    [Category("变量参数")]
    [DisplayName("变量名")]
    public string VariableName
    {
        get => _variableName;
        set => SetModuleProperty(ref _variableName, value);
    }

    [Category("变量参数")]
    [DisplayName("值表达式")]
    public string ValueExpression
    {
        get => _valueExpression;
        set => SetModuleProperty(ref _valueExpression, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(VariableName))
        {
            return Task.FromResult(ModuleResult.Fail("变量名不能为空"));
        }

        var evaluator = new SimpleExpressionEvaluator();
        if (!evaluator.TryEvaluate(ValueExpression, context.Variables, out var value, out var error))
        {
            return Task.FromResult(ModuleResult.Fail($"表达式错误: {error}"));
        }

        try
        {
            context.Variables.SetVariable(VariableName, NormalizeValue(value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ModuleResult.Fail(ex.Message));
        }

        context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Info, Param.ModuleName, $"设置变量 {VariableName} = {FormatValue(value)}，表达式: {ValueExpression}");
        return Task.FromResult(ModuleResult.Ok("变量设置完成"));
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is double d && Math.Abs(d % 1) < 1e-12)
        {
            return (int)d;
        }

        return value;
    }

    private static string FormatValue(object? value) => value?.ToString() ?? "null";
}
