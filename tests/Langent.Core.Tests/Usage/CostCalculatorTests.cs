using Langent.Core.Settings;
using Langent.Core.Usage;
using Xunit;

namespace Langent.Core.Tests.Usage;

public sealed class CostCalculatorTests
{
    private static readonly IReadOnlyDictionary<string, ModelPrice> Prices = new Dictionary<string, ModelPrice>
    {
        ["m"] = new ModelPrice(2.00m, 10.00m),
    };

    [Fact]
    public void Computes_known_value()
    {
        var result = CostCalculator.Compute("m", inputTokens: 1_000_000, outputTokens: 500_000, Prices);

        Assert.False(result.PriceMissing);
        Assert.Equal(7.00m, result.CostUsd); // 2.0 + 5.0
    }

    [Fact]
    public void Rounds_to_six_decimals()
    {
        var result = CostCalculator.Compute("m", inputTokens: 1, outputTokens: 1, Prices);

        Assert.Equal(0.000012m, result.CostUsd); // 0.000002 + 0.00001
    }

    [Fact]
    public void Missing_price_yields_zero_and_flag()
    {
        var result = CostCalculator.Compute("unknown", 1000, 1000, Prices);

        Assert.True(result.PriceMissing);
        Assert.Equal(0m, result.CostUsd);
    }
}
