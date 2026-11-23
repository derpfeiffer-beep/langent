namespace Langent.Core.Usage;

/// <summary>Short, human-friendly model names for the footer (SPEC §8.1).</summary>
public static class ModelNames
{
    public static string ShortName(string model)
    {
        if (model.Contains("haiku", StringComparison.OrdinalIgnoreCase))
        {
            return "Haiku";
        }

        if (model.Contains("sonnet", StringComparison.OrdinalIgnoreCase))
        {
            return "Sonnet";
        }

        if (model.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            return "Opus";
        }

        return model;
    }
}
