namespace MotionStudio.Motion.Abstractions;

public enum HomeReferenceMode
{
    LimitHome = 11,
    Home = 20
}

public enum HomeSearchDirection
{
    Negative = -1,
    Positive = 1
}

public enum HomeEncoderDirection
{
    Positive = 0,
    Negative = 1
}

public enum HomeSignalLevel
{
    High = 0,
    Low = 1
}

public enum HomeCaptureEdge
{
    Falling = 0,
    Rising = 1
}

public sealed record HomeMotionOptions
{
    public HomeReferenceMode Mode { get; init; } = HomeReferenceMode.Home;

    public HomeSearchDirection SearchDirection { get; init; } = HomeSearchDirection.Positive;

    public HomeEncoderDirection EncoderDirection { get; init; } = HomeEncoderDirection.Positive;

    public HomeSignalLevel PositiveLimitTriggerLevel { get; init; } = HomeSignalLevel.Low;

    public HomeSignalLevel NegativeLimitTriggerLevel { get; init; } = HomeSignalLevel.Low;

    public HomeCaptureEdge CaptureEdge { get; init; } = HomeCaptureEdge.Falling;

    public double VelHigh { get; init; } = 30d;

    public double VelLow { get; init; } = 1d;

    public double Acc { get; init; } = 80d;

    public double Dec { get; init; } = 80d;

    public long SearchHomeDistance { get; init; } = 200000L;

    public long EscapeStep { get; init; } = 1000L;

    public double Timeout { get; init; } = 30d;
}
