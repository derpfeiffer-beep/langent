using Langent.Core.Settings;

namespace Langent.Core.Usage;

/// <summary>Result of a cost computation (SPEC §7).</summary>
public sealed record CostResult(decimal CostUsd, bool PriceMissing);

/// <summary>Computes translation cost from token counts and per-model prices (SPEC §7).</summary>
public static class CostCalculator
{
    public static CostResult Compute(
        string model,
        int inputTokens,
        int outputTokens,
        IReadOnlyDictionary<string, ModelPrice> prices)
    {
        if (!prices.TryGetValue(model, out var price))
        {
            return new CostResult(0m, PriceMissing: true);
        }

        var cost = inputTokens / 1_000_000m * price.InputPerMTokUsd
            + outputTokens / 1_000_000m * price.OutputPerMTokUsd;

        return new CostResult(Math.Round(cost, 6, MidpointRounding.AwayFromZero), PriceMissing: false);
    }
}
