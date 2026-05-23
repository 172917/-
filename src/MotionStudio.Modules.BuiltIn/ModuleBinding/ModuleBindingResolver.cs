using MotionStudio.Core.Modules;
using MotionStudio.Motion.Config;

namespace MotionStudio.Modules.BuiltIn.ModuleBinding;

internal static class ModuleBindingResolver
{
    public static bool TryResolveAxis(
        MotionContext context,
        string axisName,
        int axisNo,
        string motionCardName,
        out AxisBinding binding,
        out string error)
    {
        binding = default;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(axisName))
        {
            var axis = context.MotionConfigService.GetAxisByName(axisName);
            if (axis is null)
            {
                error = $"轴配置不存在: {axisName}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(axis.MotionCardName))
            {
                error = $"轴配置缺少运动卡名称: {axisName}";
                return false;
            }

            binding = new AxisBinding(
                axis.AxisName,
                axis.AxisNo,
                axis.MotionCardName,
                axis.VelocityRatio,
                axis.HomeTimeout,
                axis.AbsVelocity,
                axis.AbsAcceleration,
                axis.AbsDeceleration,
                axis.RelVelocity,
                axis.RelAcceleration,
                axis.RelDeceleration);
            return true;
        }

        if (axisNo <= 0)
        {
            error = "轴号必须大于 0";
            return false;
        }

        var fallbackCardName = string.IsNullOrWhiteSpace(motionCardName) ? MotionContext.DefaultMotionCardName : motionCardName;
        binding = new AxisBinding(string.Empty, axisNo, fallbackCardName, 0.5, 30, 50, 100, 100, 50, 100, 100);
        return true;
    }

    public static bool TryResolveInput(
        MotionContext context,
        string inputName,
        string fallbackMotionCardName,
        out IoBinding binding,
        out string error)
    {
        return TryResolveIo(context, inputName, fallbackMotionCardName, false, out binding, out error);
    }

    public static bool TryResolveOutput(
        MotionContext context,
        string outputName,
        string fallbackMotionCardName,
        out IoBinding binding,
        out string error)
    {
        return TryResolveIo(context, outputName, fallbackMotionCardName, true, out binding, out error);
    }

    private static bool TryResolveIo(
        MotionContext context,
        string ioName,
        string fallbackMotionCardName,
        bool isOutput,
        out IoBinding binding,
        out string error)
    {
        binding = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ioName))
        {
            error = $"{(isOutput ? "DO" : "DI")} 名称不能为空";
            return false;
        }

        var io = isOutput
            ? context.MotionConfigService.GetOutputByName(ioName)
            : context.MotionConfigService.GetInputByName(ioName);
        if (io is null)
        {
            error = $"{(isOutput ? "DO" : "DI")} 配置不存在: {ioName}";
            return false;
        }

        var motionCardName = ResolveIoMotionCardName(context, io, fallbackMotionCardName);
        if (string.IsNullOrWhiteSpace(motionCardName))
        {
            error = $"IO 配置缺少运动卡映射: {ioName}";
            return false;
        }

        var pointName = BuildPointName(io, isOutput);
        binding = new IoBinding(io.Name, pointName, motionCardName);
        return true;
    }

    private static string ResolveIoMotionCardName(MotionContext context, IOConfig io, string fallbackMotionCardName)
    {
        if (!string.IsNullOrWhiteSpace(io.MotionCardName))
        {
            return io.MotionCardName;
        }

        var byIndex = context.TryGetMotionCardNameByIndex(io.CardIndex);
        if (!string.IsNullOrWhiteSpace(byIndex))
        {
            return byIndex;
        }

        return string.IsNullOrWhiteSpace(fallbackMotionCardName) ? MotionContext.DefaultMotionCardName : fallbackMotionCardName;
    }

    private static string BuildPointName(IOConfig io, bool isOutput)
    {
        if (io.Name.StartsWith("DI", StringComparison.OrdinalIgnoreCase)
            || io.Name.StartsWith("DO", StringComparison.OrdinalIgnoreCase))
        {
            return io.Name;
        }

        var prefix = isOutput ? "DO" : "DI";
        return prefix + io.PointNo;
    }
}

internal readonly record struct AxisBinding(
    string AxisName,
    int AxisNo,
    string MotionCardName,
    double VelocityRatio,
    double HomeTimeout,
    double AbsVelocity,
    double AbsAcceleration,
    double AbsDeceleration,
    double RelVelocity,
    double RelAcceleration,
    double RelDeceleration);

internal readonly record struct IoBinding(
    string ConfigName,
    string PointName,
    string MotionCardName);
