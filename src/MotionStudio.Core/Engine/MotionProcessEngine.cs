using System.Diagnostics;
using MotionStudio.Core.Logging;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Project;
using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Core.Engine;

/// <summary>
/// 运动流程执行引擎，负责顺序执行、安全检查、停止、急停和失败停轴。
/// </summary>
public sealed class MotionProcessEngine
{
    private MotionModuleBase? _currentModule;

    public event EventHandler<MotionModuleBase>? ModuleStarted;

    public event EventHandler<MotionModuleBase>? ModuleFinished;

    public event EventHandler? RuntimeStateChanged;

    /// <summary>
    /// 按项目模块顺序执行流程。
    /// </summary>
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
            foreach (var module in project.Modules.ToList())
            {
                token.ThrowIfCancellationRequested();
                var safety = await CheckSafetyAsync(context, module).ConfigureAwait(false);
                if (!safety.Success)
                {
                    await context.StopAllMotionCardsAsync(true).ConfigureAwait(false);
                    context.Logger.Write(LogLevel.Error, "Engine", safety.Message);
                    return safety;
                }

                if (!module.Param.IsEnabled)
                {
                    context.Logger.Write(LogLevel.Info, module.Param.ModuleName, "模块已禁用，跳过执行");
                    continue;
                }

                _currentModule = module;
                context.RuntimeState.CurrentModuleName = module.Param.ModuleName;
                module.Param.IsRunning = true;
                module.Param.IsSuccess = false;
                module.Param.Message = "运行中";
                ModuleStarted?.Invoke(this, module);

                var watch = Stopwatch.StartNew();
                context.Logger.Write(LogLevel.Info, module.Param.ModuleName, $"开始执行，运动卡：{module.Param.MotionCardName}");

                ModuleResult result;
                try
                {
                    result = await module.ExecuteAsync(context, token).ConfigureAwait(false);
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

                if (!result.Success)
                {
                    await context.StopAllMotionCardsAsync(true).ConfigureAwait(false);
                    context.Logger.Write(LogLevel.Error, module.Param.ModuleName, result.Message);
                    return result;
                }

                context.Logger.Write(LogLevel.Success, module.Param.ModuleName, $"执行完成，耗时 {module.Param.CostTimeMs} ms");
            }

            return ModuleResult.Ok("流程执行完成");
        }
        catch (OperationCanceledException)
        {
            context.RuntimeState.IsStopRequested = true;
            if (_currentModule is not null)
            {
                await _currentModule.StopAsync(context).ConfigureAwait(false);
            }

            await context.StopAllMotionCardsAsync(false).ConfigureAwait(false);
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

    /// <summary>
    /// 单步执行一个模块。
    /// </summary>
    public async Task<ModuleResult> RunSingleAsync(MotionModuleBase module, MotionContext context, CancellationToken token)
    {
        var tempProject = new MotionProject { ProjectName = "SingleStep" };
        tempProject.Modules.Add(module);
        return await RunAsync(tempProject, context, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 急停当前流程和所有注册运动卡。
    /// </summary>
    public async Task EmergencyStopAsync(MotionContext context)
    {
        context.RuntimeState.IsEmergencyStop = true;
        await context.StopAllMotionCardsAsync(true).ConfigureAwait(false);
        context.Logger.Write(LogLevel.Error, "Engine", "急停已触发");
        RuntimeStateChanged?.Invoke(this, EventArgs.Empty);
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

        IMotionCard card;
        try
        {
            card = context.GetMotionCard(module.Param.MotionCardName);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ModuleResult.Fail(ex.Message));
        }

        if (!card.IsConnected)
        {
            return Task.FromResult(ModuleResult.Fail($"运动卡 {module.Param.MotionCardName} 未连接"));
        }

        foreach (var property in module.GetType().GetProperties().Where(p => p.Name.EndsWith("AxisNo", StringComparison.OrdinalIgnoreCase)))
        {
            if (property.GetValue(module) is not int axisNo || axisNo < 0)
            {
                continue;
            }

            var state = card.GetAxisState(axisNo);
            if (state.Alarm || state.PositiveLimit || state.NegativeLimit)
            {
                context.RuntimeState.AlarmCount++;
                return Task.FromResult(ModuleResult.Fail($"运动卡 {module.Param.MotionCardName} 轴号 {axisNo} 报警或限位触发"));
            }
        }

        return Task.FromResult(ModuleResult.Ok());
    }
}
