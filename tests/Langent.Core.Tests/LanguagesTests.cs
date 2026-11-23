using Langent.Core;
using Xunit;

namespace Langent.Core.Tests;

public sealed class LanguagesTests
{
    [Fact]
    public void Contains_the_24_spec_languages()
    {
        Assert.Equal(24, Languages.All.Count);
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        Assert.True(Languages.TryGet("DE", out var language));
        Assert.Equal("German", language.EnglishName);
        Assert.Equal("Deutsch", language.GermanName);
    }

    [Fact]
    public void Unknown_code_is_not_found()
    {
        Assert.False(Languages.TryGet("xx", out _));
        Assert.Null(Languages.Find("xx"));
        Assert.Equal("xx", Languages.EnglishNameOrCode("xx"));
    }
}
