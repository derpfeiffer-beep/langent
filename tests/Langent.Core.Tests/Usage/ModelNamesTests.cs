using Langent.Core.Usage;
using Xunit;

namespace Langent.Core.Tests.Usage;

public sealed class ModelNamesTests
{
    [Theory]
    [InlineData("claude-haiku-5-5", "Haiku")]
    [InlineData("claude-sonnet-5-5", "Sonnet")]
    [InlineData("claude-opus-5-5", "Opus")]
    [InlineData("some-other-model", "some-other-model")]
    public void ShortName_maps_known_families(string model, string expected)
    {
        Assert.Equal(expected, ModelNames.ShortName(model));
    }
}
