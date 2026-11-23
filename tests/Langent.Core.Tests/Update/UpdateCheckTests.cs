using Langent.Core.Update;
using Xunit;

namespace Langent.Core.Tests.Update;

public sealed class UpdateCheckTests
{
    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("v0.1.0", "0.1.0", false)]
    [InlineData("v0.1.0", "0.2.0", false)]
    [InlineData("v1.0", "1.0.0.0", false)]
    [InlineData("not-a-version", "0.1.0", false)]
    [InlineData("", "0.1.0", false)]
    public void IsNewer_compares_versions(string tag, string current, bool expected)
    {
        Assert.Equal(expected, UpdateCheck.IsNewer(tag, Version.Parse(current)));
    }

    [Fact]
    public void ShouldCheck_when_never_checked()
    {
        Assert.True(UpdateCheck.ShouldCheckToday(null, new DateTime(2026, 11, 23)));
    }

    [Fact]
    public void ShouldCheck_when_last_check_was_yesterday()
    {
        Assert.True(UpdateCheck.ShouldCheckToday(new DateTime(2026, 11, 22, 23, 0, 0), new DateTime(2026, 11, 23, 1, 0, 0)));
    }

    [Fact]
    public void Should_not_check_again_same_day()
    {
        Assert.False(UpdateCheck.ShouldCheckToday(new DateTime(2026, 11, 23, 8, 0, 0), new DateTime(2026, 11, 23, 20, 0, 0)));
    }
}
