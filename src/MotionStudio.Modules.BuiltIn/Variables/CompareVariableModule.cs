using System.ComponentModel;
using MotionStudio.Core.Expressions;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Variables;

[Category("变量计算")]
[DisplayName("变量比较")]
[Description("比较表达式并写入布尔变量")]
[MotionModuleIcon("Cmp")]
public sealed class CompareVariableModule : MotionModuleBase
{
    private string _resultVariableName = "isOk";
    private string _expression = "1 > 0";

    [Category("变量参数")]
    [DisplayName("结果变量名")]
    public string ResultVariableName
    {
        get => _resultVariableName;
        set => SetModuleProperty(ref _resultVariableName, value);
    }

    [Category("变量参数")]
    [DisplayName("比较表达式")]
    public string Expression
    {
        get => _expression;
        set => SetModuleProperty(ref _expression, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ResultVariableName))
        {
            return Task.FromResult(ModuleResult.Fail("结果变量名不能为空"));
        }

        var evaluator = new SimpleExpressionEvaluator();
        if (!evaluator.TryEvaluate(Expression, context.Variables, out var value, out var error))
        {
            return Task.FromResult(ModuleResult.Fail($"表达式错误: {error}"));
        }

        if (value is not bool boolValue)
        {
            return Task.FromResult(ModuleResult.Fail("比较表达式结果必须是 bool"));
        }

        context.Variables.SetVariable(ResultVariableName, boolValue);
        context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Info, Param.ModuleName, $"变量比较 {ResultVariableName} = {boolValue}，表达式: {Expression}");
        return Task.FromResult(ModuleResult.Ok("变量比较完成"));
    }
}
