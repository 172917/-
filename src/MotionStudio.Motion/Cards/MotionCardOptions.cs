namespace MotionStudio.Motion.Cards;

public sealed class MotionCardOptions
{
    public string CardName { get; set; } = "Sim-1";

    public MotionCardType CardType { get; set; } = MotionCardType.Sim;

    public bool Enabled { get; set; } = true;

    public string DllPath { get; set; } = string.Empty;

    public string ConfigFilePath { get; set; } = string.Empty;

    public int AxisBaseIndex { get; set; }

    public int IoBaseIndex { get; set; }

    public string Description { get; set; } = string.Empty;
}
