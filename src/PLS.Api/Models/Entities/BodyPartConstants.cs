namespace PLS.Api.Models.Entities;

public static class BodyPartConstants
{
    public static readonly HashSet<string> ValidKeys = new()
    {
        "hals_vorne", "brust_links", "brust_rechts", "leiste_links_vorne", "leiste_rechts_vorne",
        "oberschenkel_rechts_vorne", "unterschenkel_links_vorne", "oberschenkel_links_vorne",
        "unterschenkel_rechts_vorne", "oberarm_links_vorne", "oberarm_rechts_vorne",
        "unterarm_links_vorne", "unterarm_rechts_vorne", "genital_vorne", "kopf_vorne",
        "schulter_links_vorne", "schulter_rechts_vorne", "huefte_links_vorne", "huefte_rechts_vorne",
        "knie_links_vorne", "knie_rechts_vorne", "ellbogen_rechts_vorne", "ellbogen_links_vorne",
        "fuss_rechts_vorne", "fuss_links_vorne", "auge_rechts", "auge_links", "mund",
        "hand_links_vorne", "hand_rechts_vorne",
        "kopf_hinten", "hals_hinten", "ruecken_rechts", "oberschenkel_rechts_hinten",
        "unterschenkel_links_hinten", "oberschenkel_links_hinten", "unterschenkel_rechts_hinten",
        "oberarm_links_hinten", "oberarm_rechts_hinten", "unterarm_links_hinten", "unterarm_rechts_hinten",
        "becken_links_hinten", "ruecken_links", "becken_rechts_hinten", "genital_hinten",
        "huefte_rechts_hinten", "huefte_links_hinten", "knie_links_hinten", "knie_rechts_hinten",
        "ellbogen_rechts_hinten", "ellbogen_links_hinten", "fuss_rechts_hinten", "fuss_links_hinten",
        "hand_links_hinten", "hand_rechts_hinten", "schulter_rechts_hinten", "schulter_links_hinten",
        "brustwirbel", "lendenwirbel"
    };

    public static Dictionary<string, int> CreateDefault()
    {
        return ValidKeys.ToDictionary(k => k, _ => 0);
    }
}
