using MotionStudio.Core.Communication;
using MotionStudio.Core.Engine;
using MotionStudio.Core.Logging;
using MotionStudio.Core.Services;
using MotionStudio.Core.Variables;
using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Core.Modules;

/// <summary>
/// 流程运行上下文，保存多张命名运动卡、变量表、日志和运行状态。
/// </summary>
public sealed class MotionContext
{
    public const string DefaultMotionCardName = "Sim-1";

    public Dictionary<string, IMotionCard> MotionCards { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IMotionCard MotionCard
    {
        get => GetMotionCard(DefaultMotionCardName);
        set => MotionCards[DefaultMotionCardName] = value;
    }

    public MotionVariableTable Variables { get; set; } = new();

    public ILogService Logger { get; set; } = default!;

    public MotionRuntimeState RuntimeState { get; set; } = new();

    public MotionConfigService MotionConfigService { get; set; } = new();

    public ITcpClientService TcpClientService { get; set; } = new TcpClientService();

    public ISerialPortService SerialPortService { get; set; } = new SerialPortService();

    public IMotionCard GetMotionCard(string? cardName)
    {
        var resolvedName = string.IsNullOrWhiteSpace(cardName) ? DefaultMotionCardName : cardName;
        if (!MotionCards.TryGetValue(resolvedName, out var card))
        {
            throw new InvalidOperationException($"未找到运动卡实例：{resolvedName}");
        }

        return card;
    }

    public bool AreAllMotionCardsConnected()
    {
        return MotionCards.Count > 0 && MotionCards.Values.All(card => card.IsConnected);
    }

    public string? TryGetMotionCardNameByIndex(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= MotionCards.Count)
        {
            return null;
        }

        return MotionCards.Keys.ElementAt(cardIndex);
    }

    public async Task StopAllMotionCardsAsync(bool emergency)
    {
        foreach (var card in MotionCards.Values.Distinct())
        {
            await card.StopAllAsync(emergency).ConfigureAwait(false);
        }
    }
}
