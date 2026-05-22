namespace MotionStudio.Motion.Abstractions;

/// <summary>
/// Exposes per-operation native API trace for diagnostics.
/// </summary>
public interface IApiTraceProvider
{
    void BeginApiTraceScope();

    string ConsumeApiTrace();
}
