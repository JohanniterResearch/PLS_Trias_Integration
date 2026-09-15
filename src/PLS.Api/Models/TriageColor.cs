namespace PLS.Api.Models;

public static class TriageColor
{
    public const string Rot = "rot";
    public const string Gelb = "gelb";
    public const string Gruen = "grün";
    public const string Blau = "blau";
    public const string Schwarz = "schwarz";

    public static readonly string[] AllowedValues = [Rot, Gelb, Gruen, Blau, Schwarz];

    public static bool TryNormalize(string? value, out string? normalized)
    {
        if (value is null)
        {
            normalized = null;
            return true;
        }

        var candidate = value.Trim().ToLowerInvariant();
        if (AllowedValues.Contains(candidate, StringComparer.Ordinal))
        {
            normalized = candidate;
            return true;
        }

        normalized = null;
        return false;
    }
}
