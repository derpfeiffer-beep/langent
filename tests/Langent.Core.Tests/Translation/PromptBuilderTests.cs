using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Translation;

public sealed class PromptBuilderTests
{
    private static string Build(
        Tone tone = Tone.Neutral,
        string? source = null,
        IReadOnlyList<GlossaryEntry>? glossary = null,
        string custom = "")
        => PromptBuilder.BuildSystemPrompt("German", "English", source, tone, glossary ?? [], custom);

    [Fact]
    public void Without_glossary_or_instructions_those_sections_are_omitted()
    {
        var prompt = Build();

        Assert.DoesNotContain("Glossary", prompt);
        Assert.DoesNotContain("Additional instructions", prompt);
        Assert.Contains("into German", prompt);
        Assert.Contains("into English instead", prompt);
    }

    [Fact]
    public void Glossary_with_empty_target_renders_keep_unchanged()
    {
        var prompt = Build(glossary: [new GlossaryEntry("proManager", "")]);

        Assert.Contains("Glossary (always use these translations):", prompt);
        Assert.Contains("- \"proManager\" -> keep unchanged", prompt);
    }

    [Fact]
    public void Glossary_with_target_renders_mapping()
    {
        var prompt = Build(glossary: [new GlossaryEntry("cat", "Katze")]);

        Assert.Contains("- \"cat\" -> \"Katze\"", prompt);
    }

    [Fact]
    public void Custom_instructions_section_appears_when_set()
    {
        var prompt = Build(custom: "Be concise.");

        Assert.Contains("Additional instructions:", prompt);
        Assert.Contains("Be concise.", prompt);
    }

    [Fact]
    public void Source_override_line_only_when_source_set()
    {
        Assert.DoesNotContain("The source language is", Build());
        Assert.Contains("The source language is French.", Build(source: "French"));
    }

    [Theory]
    [InlineData(Tone.Formal, "formal (use formal address")]
    [InlineData(Tone.Informal, "informal (use informal address")]
    [InlineData(Tone.Neutral, "keep the tone of the original")]
    public void Tone_instruction_matches(Tone tone, string expected)
    {
        Assert.Contains(expected, Build(tone));
    }
}
