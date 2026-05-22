using System.ComponentModel;
using MotionStudio.Core.Expressions;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Logic;

[Category("逻辑控制")]
[DisplayName("如果")]
[Description("条件分支开始")]
[MotionModuleIcon("If")]
public sealed class IfModule : MotionModuleBase
{
    private string _conditionExpression = "true";

    public IfModule()
    {
        ModuleKind = ModuleKind.If;
    }

    [Category("逻辑参数")]
    [DisplayName("条件表达式")]
    public string ConditionExpression
    {
        get => _conditionExpression;
        set => SetModuleProperty(ref _conditionExpression, value);
    }

    [Browsable(false)]
    [Obsolete("Use ConditionExpression instead")]
    public string Expression
    {
        get => ConditionExpression;
        set => ConditionExpression = value;
    }

    [Browsable(false)]
    public bool? LastConditionResult { get; private set; }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        LastConditionResult = null;
        var evaluator = new SimpleExpressionEvaluator();
        if (!evaluator.TryEvaluate(ConditionExpression, context.Variables, out var value, out var error))
        {
            return Task.FromResult(ModuleResult.Fail($"条件表达式错误: {error}"));
        }

        if (value is not bool boolValue)
        {
            return Task.FromResult(ModuleResult.Fail("If 条件结果必须是 bool"));
        }

        LastConditionResult = boolValue;
        context.Logger.Write(MotionStudio.Core.Logging.LogLevel.Info, Param.ModuleName, $"If 条件 = {boolValue}，表达式: {ConditionExpression}");
        return Task.FromResult(ModuleResult.Ok(boolValue ? "条件成立" : "条件不成立"));
    }
}
