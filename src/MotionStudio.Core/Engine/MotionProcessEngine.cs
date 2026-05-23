using System.Diagnostics;
using MotionStudio.Core.Expressions;
using MotionStudio.Core.Logging;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Project;
using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Core.Engine;

public sealed class MotionProcessEngine
{
    private MotionModuleBase? _currentModule;

    public event EventHandler<MotionModuleBase>? ModuleStarted;

    public event EventHandler<MotionModuleBase>? ModuleFinished;

    public event EventHandler? RuntimeStateChanged;

    public int MaxLoopIterations { get; set; } = 10000;

    public async Task<ModuleResult> RunAsync(MotionProject project, MotionContext context, CancellationToken token)
    {
        if (context.MotionCards.Count == 0)
        {
            return ModuleResult.Fail("未注册运动卡实例");
        }

        context.RuntimeState.ResetForRun();
        context.RuntimeState.IsRunning = true;
        context.RuntimeState.MotionCardConnected = context.AreAllMotionCardsConnected();
        RuntimeStateChanged?.Invoke(this, EventArgs.Empty);

        var totalWatch = Stopwatch.StartNew();
        try
        {
            var modules = project.Modules.ToList();
            if (!TryBuildLoopBindings(modules, out var loopBindings, out var loopError))
            {
                context.Logger.Write(LogLevel.Error, "Engine", loopError);
                return ModuleResult.Fail(loopError);
            }

            var loopStates = new Dictionary<int, LoopRuntimeState>();
            var index = 0;
            while (index < modules.Count)
            {
                token.ThrowIfCancellationRequested();
                var module = modules[index];

                if (module.ModuleKind == ModuleKind.EndIf)
                {
                    index++;
                    continue;
                }

                if (module.ModuleKind == ModuleKind.LoopEnd)
                {
                    var loopEndResult = HandleLoopEnd(module, modules, context, loopBindings, loopStates, ref index);
                    if (!loopEndResult.Success)
                    {
                        await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
                        context.Logger.Write(LogLevel.Error, module.Param.ModuleName, loopEndResult.Message);
                        return loopEndResult;
                    }

                    continue;
                }

                if (module.ModuleKind == ModuleKind.Loop)
                {
                    var loopStartResult = InitializeLoopStart(module, index, context, loopBindings, loopStates, ref index);
                    if (!loopStartResult.Success)
                    {
                        await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
                        context.Logger.Write(LogLevel.Error, module.Param.ModuleName, loopStartResult.Message);
                        return loopStartResult;
                    }

                    if (loopStartResult.Message == "SKIP_LOOP_BODY")
                    {
                        continue;
                    }
                }

                var safety = await CheckSafetyAsync(context, module).ConfigureAwait(true);
                if (!safety.Success)
                {
                    await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
                    context.Logger.Write(LogLevel.Error, "Engine", safety.Message);
                    return safety;
                }

                if (!module.Param.IsEnabled)
                {
                    context.Logger.Write(LogLevel.Info, module.Param.ModuleName, "模块已禁用，跳过执行");
                    index++;
                    continue;
                }

                var result = await ExecuteModuleCoreAsync(module, context, token).ConfigureAwait(true);
                if (!result.Success)
                {
                    await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
                    return result;
                }

                if (module.ModuleKind == ModuleKind.If)
                {
                    var condition = GetIfConditionResult(module);
                    if (condition is null)
                    {
                        await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
                        var message = $"If 模块未返回有效布尔结果: {module.Param.ModuleName}";
                        context.Logger.Write(LogLevel.Error, "Engine", message);
                        return ModuleResult.Fail(message);
                    }

                    if (!condition.Value)
                    {
                        var endIfIndex = FindMatchingEndIfIndex(modules, index);
                        if (endIfIndex < 0)
                        {
                            await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
                            var message = $"If 未找到匹配 EndIf: {module.Param.ModuleName}";
                            context.Logger.Write(LogLevel.Error, "Engine", message);
                            return ModuleResult.Fail(message);
                        }

                        context.Logger.Write(LogLevel.Warning, module.Param.ModuleName, "条件不成立，跳过 If 块");
                        for (var skip = index + 1; skip < endIfIndex; skip++)
                        {
                            var skipped = modules[skip];
                            skipped.Param.Message = "Skipped(If条件不成立)";
                            skipped.Param.IsRunning = false;
                            context.Logger.Write(LogLevel.Info, skipped.Param.ModuleName, "被 If 条件跳过");
                        }

                        index = endIfIndex + 1;
                        continue;
                    }
                }

                index++;
            }

            return ModuleResult.Ok("流程执行完成");
        }
        catch (OperationCanceledException)
        {
            context.RuntimeState.IsStopRequested = true;
            if (_currentModule is not null)
            {
                await _currentModule.StopAsync(context).ConfigureAwait(true);
            }

            await context.StopAllMotionCardsAsync(false).ConfigureAwait(true);
            context.Logger.Write(LogLevel.Warning, "Engine", "流程已停止");
            return ModuleResult.Fail("流程已停止");
        }
        finally
        {
            totalWatch.Stop();
            context.RuntimeState.TotalCostTimeMs = (int)totalWatch.ElapsedMilliseconds;
            context.RuntimeState.CurrentModuleName = string.Empty;
            context.RuntimeState.IsRunning = false;
            context.RuntimeState.MotionCardConnected = context.AreAllMotionCardsConnected();
            _currentModule = null;
            RuntimeStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<ModuleResult> RunSingleAsync(MotionModuleBase module, MotionContext context, CancellationToken token)
    {
        var tempProject = new MotionProject { ProjectName = "SingleStep" };
        tempProject.Modules.Add(module);
        return await RunAsync(tempProject, context, token).ConfigureAwait(true);
    }

    public async Task EmergencyStopAsync(MotionContext context)
    {
        context.RuntimeState.IsEmergencyStop = true;
        await context.StopAllMotionCardsAsync(true).ConfigureAwait(true);
        context.Logger.Write(LogLevel.Error, "Engine", "急停已触发");
        RuntimeStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private ModuleResult InitializeLoopStart(
        MotionModuleBase module,
        int index,
        MotionContext context,
        LoopBindings bindings,
        Dictionary<int, LoopRuntimeState> loopStates,
        ref int runtimeIndex)
    {
        if (loopStates.ContainsKey(index))
        {
            return ModuleResult.Ok();
        }

        if (!bindings.StartToEnd.TryGetValue(index, out var endIndex))
        {
            return ModuleResult.Fail($"循环开始未找到匹配循环结束: {module.Param.ModuleName}");
        }

        var state = new LoopRuntimeState
        {
            StartIndex = index,
            EndIndex = endIndex,
            ModeName = GetStringProperty(module, "LoopMode", "Count"),
            LoopCount = GetIntProperty(module, "LoopCount", 0),
            ConditionExpression = GetStringProperty(module, "ConditionExpression", "true"),
            CurrentIndexVariableName = GetStringProperty(module, "CurrentIndexVariableName", string.Empty),
            CurrentIteration = 0
        };

        if (IsCountMode(state) && state.LoopCount <= 0)
        {
            context.Logger.Write(LogLevel.Info, module.Param.ModuleName, "循环次数<=0，跳过循环体");
            runtimeIndex = endIndex + 1;
            return ModuleResult.Ok("SKIP_LOOP_BODY");
        }

        if (!IsCountMode(state))
        {
            var conditionCheck = EvaluateLoopCondition(context, state);
            if (!conditionCheck.Success)
            {
                return ModuleResult.Fail(conditionCheck.Error);
            }

            if (!conditionCheck.Value)
            {
                context.Logger.Write(LogLevel.Info, module.Param.ModuleName, "循环条件初次为 false，跳过循环体");
                runtimeIndex = endIndex + 1;
                return ModuleResult.Ok("SKIP_LOOP_BODY");
            }
        }

        if (!TryMarkIteration(context, module, state, 0, out var markError))
        {
            return ModuleResult.Fail(markError);
        }

        loopStates[index] = state;
        context.Logger.Write(LogLevel.Info, module.Param.ModuleName, $"循环开始，模式={state.ModeName}");
        return ModuleResult.Ok();
    }

    private ModuleResult HandleLoopEnd(
        MotionModuleBase loopEndModule,
        IReadOnlyList<MotionModuleBase> modules,
        MotionContext context,
        LoopBindings bindings,
        Dictionary<int, LoopRuntimeState> loopStates,
        ref int runtimeIndex)
    {
        if (!bindings.EndToStart.TryGetValue(runtimeIndex, out var startIndex))
        {
            return ModuleResult.Fail($"孤立循环结束: {loopEndModule.Param.ModuleName}");
        }

        if (!loopStates.TryGetValue(startIndex, out var state))
        {
            return ModuleResult.Fail($"循环上下文不存在: {loopEndModule.Param.ModuleName}");
        }

        var startModule = modules[startIndex];

        bool shouldContinue;
        if (IsCountMode(state))
        {
            var nextIteration = state.CurrentIteration + 1;
            if (nextIteration >= MaxLoopIterations)
            {
                return ModuleResult.Fail($"循环超过最大次数限制 {MaxLoopIterations}: {startModule.Param.ModuleName}");
            }

            shouldContinue = nextIteration < state.LoopCount;
            if (shouldContinue)
            {
                state.CurrentIteration = nextIteration;
            }
        }
        else
        {
            var conditionCheck = EvaluateLoopCondition(context, state);
            if (!conditionCheck.Success)
            {
                return ModuleResult.Fail(conditionCheck.Error);
            }

            shouldContinue = conditionCheck.Value;
            if (shouldContinue)
            {
                var nextIteration = state.CurrentIteration + 1;
                if (nextIteration >= MaxLoopIterations)
                {
                    return ModuleResult.Fail($"循环超过最大次数限制 {MaxLoopIterations}: {startModule.Param.ModuleName}");
                }

                state.CurrentIteration = nextIteration;
            }
            else
            {
                context.Logger.Write(LogLevel.Info, startModule.Param.ModuleName, "循环条件为 false，结束循环");
            }
        }

        if (shouldContinue)
        {
            if (!TryMarkIteration(context, startModule, state, state.CurrentIteration, out var markError))
            {
                return ModuleResult.Fail(markError);
            }

            context.Logger.Write(LogLevel.Info, startModule.Param.ModuleName, $"循环第 {state.CurrentIteration + 1} 次");
            runtimeIndex = startIndex + 1;
            return ModuleResult.Ok();
        }

        loopStates.Remove(startIndex);
        context.Logger.Write(LogLevel.Info, startModule.Param.ModuleName, "循环结束");
        runtimeIndex++;
        return ModuleResult.Ok();
    }

    private static LoopConditionCheck EvaluateLoopCondition(MotionContext context, LoopRuntimeState state)
    {
        var evaluator = new SimpleExpressionEvaluator();
        if (!evaluator.TryEvaluate(state.ConditionExpression, context.Variables, out var value, out var error))
        {
            return LoopConditionCheck.Fail($"循环条件表达式错误: {error}");
        }

        if (value is not bool boolValue)
        {
            return LoopConditionCheck.Fail("循环条件表达式结果必须是 bool");
        }

        return LoopConditionCheck.Ok(boolValue);
    }

    private static bool TryMarkIteration(MotionContext context, MotionModuleBase startModule, LoopRuntimeState state, int iteration, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(state.CurrentIndexVariableName))
        {
            return true;
        }

        try
        {
            context.Variables.SetVariable(state.CurrentIndexVariableName, iteration);
            context.Logger.Write(LogLevel.Info, startModule.Param.ModuleName, $"循环索引变量 {state.CurrentIndexVariableName}={iteration}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsCountMode(LoopRuntimeState state)
    {
        return state.ModeName.Equals("Count", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryBuildLoopBindings(IReadOnlyList<MotionModuleBase> modules, out LoopBindings bindings, out string error)
    {
        bindings = new LoopBindings();
        error = string.Empty;

        var stack = new Stack<int>();
        for (var i = 0; i < modules.Count; i++)
        {
            var kind = modules[i].ModuleKind;
            if (kind == ModuleKind.Loop)
            {
                stack.Push(i);
                continue;
            }

            if (kind != ModuleKind.LoopEnd)
            {
                continue;
            }

            if (stack.Count == 0)
            {
                error = $"存在孤立循环结束模块: {modules[i].Param.ModuleName}";
                return false;
            }

            var start = stack.Pop();
            bindings.StartToEnd[start] = i;
            bindings.EndToStart[i] = start;
        }

        if (stack.Count > 0)
        {
            var start = stack.Peek();
            error = $"循环开始未找到匹配循环结束: {modules[start].Param.ModuleName}";
            return false;
        }

        return true;
    }

    private async Task<ModuleResult> ExecuteModuleCoreAsync(MotionModuleBase module, MotionContext context, CancellationToken token)
    {
        _currentModule = module;
        context.RuntimeState.CurrentModuleName = module.Param.ModuleName;
        module.Param.IsRunning = true;
        module.Param.IsSuccess = false;
        module.Param.Message = "运行中";
        ModuleStarted?.Invoke(this, module);

        var watch = Stopwatch.StartNew();
        BeginApiTraceScope(context.MotionCards.Values);
        context.Logger.Write(LogLevel.Info, module.Param.ModuleName, $"开始执行，运动卡：{module.Param.MotionCardName}");

        ModuleResult result;
        try
        {
            result = await module.ExecuteAsync(context, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            result = ModuleResult.Fail("流程已取消");
            throw;
        }
        catch (Exception ex)
        {
            result = ModuleResult.Fail(ex.Message);
        }
        finally
        {
            watch.Stop();
            module.Param.CostTimeMs = (int)watch.ElapsedMilliseconds;
            module.Param.IsRunning = false;
        }

        module.Param.IsSuccess = result.Success;
        module.Param.Message = result.Message;
        ModuleFinished?.Invoke(this, module);

        var apiTrace = ConsumeApiTrace(context.MotionCards.Values);

        if (result.Success)
        {
            context.Logger.Write(LogLevel.Success, module.Param.ModuleName, $"执行完成，耗时 {module.Param.CostTimeMs} ms", apiTrace);
        }
        else
        {
            context.Logger.Write(LogLevel.Error, module.Param.ModuleName, result.Message, apiTrace);
        }

        return result;
    }

    private static void BeginApiTraceScope(IEnumerable<IMotionCard> cards)
    {
        foreach (var card in cards.Distinct())
        {
            if (card is IApiTraceProvider traceProvider)
            {
                traceProvider.BeginApiTraceScope();
            }
        }
    }

    private static string ConsumeApiTrace(IEnumerable<IMotionCard> cards)
    {
        var traces = new List<string>();
        foreach (var card in cards.Distinct())
        {
            if (card is not IApiTraceProvider traceProvider)
            {
                continue;
            }

            var trace = traceProvider.ConsumeApiTrace();
            if (!string.IsNullOrWhiteSpace(trace))
            {
                traces.Add(trace);
            }
        }

        return traces.Count == 0 ? string.Empty : string.Join(" | ", traces);
    }

    private static bool? GetIfConditionResult(MotionModuleBase module)
    {
        var property = module.GetType().GetProperty("LastConditionResult");
        if (property?.GetValue(module) is bool value)
        {
            return value;
        }

        return null;
    }

    private static int FindMatchingEndIfIndex(IReadOnlyList<MotionModuleBase> modules, int ifIndex)
    {
        var depth = 0;
        for (var i = ifIndex + 1; i < modules.Count; i++)
        {
            if (modules[i].ModuleKind == ModuleKind.If)
            {
                depth++;
                continue;
            }

            if (modules[i].ModuleKind != ModuleKind.EndIf)
            {
                continue;
            }

            if (depth == 0)
            {
                return i;
            }

            depth--;
        }

        return -1;
    }

    private static string GetStringProperty(MotionModuleBase module, string propertyName, string defaultValue)
    {
        var value = module.GetType().GetProperty(propertyName)?.GetValue(module);
        return value?.ToString() ?? defaultValue;
    }

    private static int GetIntProperty(MotionModuleBase module, string propertyName, int defaultValue)
    {
        var value = module.GetType().GetProperty(propertyName)?.GetValue(module);
        return value is int i ? i : defaultValue;
    }

    private static Task<ModuleResult> CheckSafetyAsync(MotionContext context, MotionModuleBase module)
    {
        if (context.RuntimeState.IsEmergencyStop)
        {
            return Task.FromResult(ModuleResult.Fail("急停状态未复位"));
        }

        if (context.RuntimeState.IsStopRequested)
        {
            return Task.FromResult(ModuleResult.Fail("流程停止请求已触发"));
        }

        var bypassAxisFaultPreCheck = module.BypassAxisFaultPreCheck;
        var cardName = module.Param.MotionCardName;
        var axisNos = new List<int>();
        var axisNameProperty = module.GetType().GetProperty("AxisName");
        var axisNoProperty = module.GetType().GetProperty("AxisNo");

        if (axisNameProperty?.GetValue(module) is string axisName && !string.IsNullOrWhiteSpace(axisName))
        {
            var axisConfig = context.MotionConfigService.GetAxisByName(axisName);
            if (axisConfig is null)
            {
                if (!bypassAxisFaultPreCheck)
                {
                    return Task.FromResult(ModuleResult.Fail($"轴配置不存在: {axisName}"));
                }
            }
            else
            {
                cardName = axisConfig.MotionCardName;
                axisNos.Add(axisConfig.AxisNo);
            }
        }
        else if (axisNoProperty?.GetValue(module) is int axisNo && axisNo > 0)
        {
            axisNos.Add(axisNo);
        }
        else
        {
            foreach (var property in module.GetType().GetProperties().Where(p => p.Name.EndsWith("AxisNo", StringComparison.OrdinalIgnoreCase)))
            {
                if (property.GetValue(module) is int value && value > 0)
                {
                    axisNos.Add(value);
                }
            }
        }

        IMotionCard card;
        try
        {
            card = context.GetMotionCard(cardName);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ModuleResult.Fail(ex.Message));
        }

        if (!card.IsConnected)
        {
            return Task.FromResult(ModuleResult.Fail($"运动卡 {cardName} 未连接"));
        }

        if (bypassAxisFaultPreCheck)
        {
            context.Logger.Write(LogLevel.Info, module.Param.ModuleName, "模块允许在轴报警/限位状态下执行");
            return Task.FromResult(ModuleResult.Ok());
        }

        foreach (var axisNo in axisNos.Distinct())
        {
            var state = card.GetAxisState(axisNo);
            if (state.Alarm || state.PositiveLimit || state.NegativeLimit)
            {
                context.RuntimeState.AlarmCount++;
                return Task.FromResult(ModuleResult.Fail($"运动卡 {cardName} 轴号 {axisNo} 报警或限位触发"));
            }
        }

        return Task.FromResult(ModuleResult.Ok());
    }

    private sealed class LoopBindings
    {
        public Dictionary<int, int> StartToEnd { get; } = new();

        public Dictionary<int, int> EndToStart { get; } = new();
    }

    private sealed class LoopRuntimeState
    {
        public int StartIndex { get; set; }

        public int EndIndex { get; set; }

        public string ModeName { get; set; } = "Count";

        public int LoopCount { get; set; }

        public string ConditionExpression { get; set; } = "true";

        public string CurrentIndexVariableName { get; set; } = string.Empty;

        public int CurrentIteration { get; set; }
    }

    private readonly struct LoopConditionCheck
    {
        public bool Success { get; }

        public bool Value { get; }

        public string Error { get; }

        private LoopConditionCheck(bool success, bool value, string error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public static LoopConditionCheck Ok(bool value) => new(true, value, string.Empty);

        public static LoopConditionCheck Fail(string error) => new(false, false, error);
    }
}

