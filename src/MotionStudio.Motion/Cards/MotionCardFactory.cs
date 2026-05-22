using MotionStudio.Motion.Abstractions;

namespace MotionStudio.Motion.Cards;

public sealed class MotionCardFactory
{
    public MotionCardBuildResult Build(IEnumerable<MotionCardOptions>? options)
    {
        var cardMap = new Dictionary<string, IMotionCard>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();

        var optionList = options?.ToList() ?? new List<MotionCardOptions>();
        if (optionList.Count == 0)
        {
            optionList.Add(CreateDefaultSimOption());
            diagnostics.Add("No motion card config found, fallback to Sim-1.");
        }

        foreach (var option in optionList)
        {
            if (string.IsNullOrWhiteSpace(option.CardName))
            {
                diagnostics.Add("Skipped unnamed motion card config.");
                continue;
            }

            if (!option.Enabled)
            {
                diagnostics.Add($"Motion card {option.CardName} is disabled, skipped.");
                continue;
            }

            if (cardMap.ContainsKey(option.CardName))
            {
                diagnostics.Add($"Duplicated motion card name {option.CardName}, kept first one.");
                continue;
            }

            var createResult = CreateCard(option);
            if (createResult.Success && createResult.Card is not null)
            {
                cardMap[option.CardName] = createResult.Card;
                diagnostics.Add($"Motion card {option.CardName} registered ({option.CardType}).");
            }
            else
            {
                diagnostics.Add(createResult.ErrorMessage ?? $"Motion card {option.CardName} creation failed.");
            }
        }

        if (cardMap.Count == 0)
        {
            var fallback = CreateDefaultSimOption();
            cardMap[fallback.CardName] = new SimMotionCard();
            diagnostics.Add("No available motion card, auto-enabled Sim-1.");
        }

        return new MotionCardBuildResult(cardMap, diagnostics);
    }

    private static MotionCardCreateResult CreateCard(MotionCardOptions option)
    {
        return option.CardType switch
        {
            MotionCardType.Sim => MotionCardCreateResult.Ok(new SimMotionCard()),
            MotionCardType.Googol => MotionCardCreateResult.Ok(new GoogolMotionCard()),
            MotionCardType.Acs => MotionCardCreateResult.Fail($"Motion card {option.CardName} is ACS, real driver is not enabled.", new FutureAcsMotionCard()),
            _ => MotionCardCreateResult.Fail($"Unknown card type for {option.CardName}: {option.CardType}.")
        };
    }

    private static MotionCardOptions CreateDefaultSimOption()
    {
        return new MotionCardOptions
        {
            CardName = "Sim-1",
            CardType = MotionCardType.Sim,
            Enabled = true,
            Description = "Default simulation motion card"
        };
    }
}

public sealed record MotionCardBuildResult(
    IReadOnlyDictionary<string, IMotionCard> MotionCards,
    IReadOnlyList<string> Diagnostics);

public sealed record MotionCardCreateResult(bool Success, IMotionCard? Card, string? ErrorMessage)
{
    public static MotionCardCreateResult Ok(IMotionCard card) => new(true, card, null);

    public static MotionCardCreateResult Fail(string errorMessage, IMotionCard? card = null) => new(false, card, errorMessage);
}
