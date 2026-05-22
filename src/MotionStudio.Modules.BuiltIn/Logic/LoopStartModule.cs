using System.ComponentModel;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Modules.BuiltIn.Logic;

public enum LoopMode
{
    Count,
    Condition
}

[Category("逻辑控制")]
[DisplayName("循环开始")]
[Description("循环控制开始")]
[MotionModuleIcon("Loop")]
public sealed class LoopStartModule : MotionModuleBase
{
    private LoopMode _loopMode = LoopMode.Count;
    private int _loopCount = 1;
    private string _conditionExpression = "true";
    private string _currentIndexVariableName = "";

    public LoopStartModule()
    {
        ModuleKind = ModuleKind.Loop;
    }

    [Category("循环参数")]
    [DisplayName("循环模式")]
    public LoopMode LoopMode
    {
        get => _loopMode;
        set => SetModuleProperty(ref _loopMode, value);
    }

    [Category("循环参数")]
    [DisplayName("循环次数")]
    public int LoopCount
    {
        get => _loopCount;
        set => SetModuleProperty(ref _loopCount, value);
    }

    [Category("循环参数")]
    [DisplayName("条件表达式")]
    public string ConditionExpression
    {
        get => _conditionExpression;
        set => SetModuleProperty(ref _conditionExpression, value);
    }

    [Category("循环参数")]
    [DisplayName("索引变量名")]
    public string CurrentIndexVariableName
    {
        get => _currentIndexVariableName;
        set => SetModuleProperty(ref _currentIndexVariableName, value);
    }

    public override Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        return Task.FromResult(ModuleResult.Ok("循环开始"));
    }
}
