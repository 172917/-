using System.ComponentModel;
using MotionStudio.Core.Expressions;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Variables;

[Category("变量计算")]
[DisplayName("变量计算")]
[Description("计算表达式并写入目标变量")]
[MotionModuleIcon("Calc")]
public sealed class CalcVariableModule : MotionModuleBase
{
    private string _targetVariableName = "result";
    private string _expression = "1+1";

    [Category("变量参数")]
    [DisplayName("目标变量名")]
    public string TargetVariableName
    {
        get => _targetVariableName;
        set => SetModuleProperty(ref _targetVariableName, value);
    }

    [Category("变量参数")]
    [DisplayName("表达式")]
    public string Expression
    {
        get => _expression;
        set => SetModuleProperty(ref _expression, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(TargetVariableName))
        {
            return Task.FromResult(ModuleResult.Fail("目标变量名不能为空"));
        }

        var evaluator = new SimpleExpressionEvaluator();
        if (!evaluator.TryEvaluate(Expression, context.Variables, out var value, out var error))
        {
            return Task.FromResult(ModuleResult.Fail($"表达式错误: {error}"));
        }

        try
        {
            context.Variables.SetVariable(TargetVariableName, NormalizeValue(value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ModuleResult.Fail(ex.Message));
        }

        context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Info, Param.ModuleName, $"变量计算 {TargetVariableName} = {FormatValue(value)}，表达式: {Expression}");
        return Task.FromResult(ModuleResult.Ok("变量计算完成"));
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
