using Langent.Core.Trigger;
using Xunit;

namespace Langent.Core.Tests.Trigger;

public sealed class AppProfilesTests
{
    [Theory]
    [InlineData("WindowsTerminal.exe")]
    [InlineData("windowsterminal.exe")]
    [InlineData("cmd.exe")]
    [InlineData("cmd")]
    [InlineData("POWERSHELL")]
    [InlineData("pwsh")]
    [InlineData("conhost")]
    [InlineData("mstsc.exe")]
    public void Default_excludes_match_case_insensitively_with_or_without_extension(string process)
    {
        var profiles = new AppProfiles();
        Assert.True(profiles.IsExcluded(process));
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("notepad.exe")]
    [InlineData("WINWORD.EXE")]
    [InlineData("chrome")]
    [InlineData("")]
    [InlineData(null)]
    public void Non_excluded_processes_are_not_excluded(string? process)
    {
        var profiles = new AppProfiles();
        Assert.False(profiles.IsExcluded(process));
    }

    [Fact]
    public void Custom_exclude_list_replaces_the_default()
    {
        var profiles = new AppProfiles(["Foo.exe"]);
        Assert.True(profiles.IsExcluded("foo"));
        Assert.False(profiles.IsExcluded("cmd"));
    }
}
