using System.Text.Json.Nodes;

namespace PLS.Api.Models;

public static class ChangeFields
{
    // Persisted IDs: never renumber or reuse. Add new fields with a new ID.
    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [1] = "patient.atmung",
        [2] = "patient.blutung",
        [3] = "patient.triagefarbe",
        [4] = "patient.location",
        [5] = "form.status",
        [100] = "body.hals_vorne",
        [101] = "body.brust_links",
        [102] = "body.brust_rechts",
        [103] = "body.leiste_links_vorne",
        [104] = "body.leiste_rechts_vorne",
        [105] = "body.oberschenkel_rechts_vorne",
        [106] = "body.unterschenkel_links_vorne",
        [107] = "body.oberschenkel_links_vorne",
        [108] = "body.unterschenkel_rechts_vorne",
        [109] = "body.oberarm_links_vorne",
        [110] = "body.oberarm_rechts_vorne",
        [111] = "body.unterarm_links_vorne",
        [112] = "body.unterarm_rechts_vorne",
        [113] = "body.genital_vorne",
        [114] = "body.kopf_vorne",
        [115] = "body.schulter_links_vorne",
        [116] = "body.schulter_rechts_vorne",
        [117] = "body.huefte_links_vorne",
        [118] = "body.huefte_rechts_vorne",
        [119] = "body.knie_links_vorne",
        [120] = "body.knie_rechts_vorne",
        [121] = "body.ellbogen_rechts_vorne",
        [122] = "body.ellbogen_links_vorne",
        [123] = "body.fuss_rechts_vorne",
        [124] = "body.fuss_links_vorne",
        [125] = "body.auge_rechts",
        [126] = "body.auge_links",
        [127] = "body.mund",
        [128] = "body.hand_links_vorne",
        [129] = "body.hand_rechts_vorne",
        [130] = "body.kopf_hinten",
        [131] = "body.hals_hinten",
        [132] = "body.ruecken_rechts",
        [133] = "body.oberschenkel_rechts_hinten",
        [134] = "body.unterschenkel_links_hinten",
        [135] = "body.oberschenkel_links_hinten",
        [136] = "body.unterschenkel_rechts_hinten",
        [137] = "body.oberarm_links_hinten",
        [138] = "body.oberarm_rechts_hinten",
        [139] = "body.unterarm_links_hinten",
        [140] = "body.unterarm_rechts_hinten",
        [141] = "body.becken_links_hinten",
        [142] = "body.ruecken_links",
        [143] = "body.becken_rechts_hinten",
        [144] = "body.genital_hinten",
        [145] = "body.huefte_rechts_hinten",
        [146] = "body.huefte_links_hinten",
        [147] = "body.knie_links_hinten",
        [148] = "body.knie_rechts_hinten",
        [149] = "body.ellbogen_rechts_hinten",
        [150] = "body.ellbogen_links_hinten",
        [151] = "body.fuss_rechts_hinten",
        [152] = "body.fuss_links_hinten",
        [153] = "body.hand_links_hinten",
        [154] = "body.hand_rechts_hinten",
        [155] = "body.schulter_rechts_hinten",
        [156] = "body.schulter_links_hinten",
        [157] = "body.brustwirbel",
        [158] = "body.lendenwirbel",
        [1000] = "form.incident.ambulanzort",
        [1001] = "form.incident.datum",
        [1002] = "form.incident.uhrzeit_beginn",
        [1003] = "form.incident.dnr_san_1",
        [1004] = "form.incident.dnr_san_2",
        [1005] = "form.incident.dnr_san_3",
        [1006] = "form.incident.dnr_na",
        [1007] = "form.incident.pls_nummer",
        [1008] = "form.incident.funkrufname",
        [1009] = "form.patient.familienname",
        [1010] = "form.patient.vorname",
        [1011] = "form.patient.geschlecht",
        [1012] = "form.patient.vers_nr",
        [1013] = "form.patient.geburtsdatum",
        [1014] = "form.patient.adresse",
        [1015] = "form.patient.staat",
        [1016] = "form.patient.telefon",
        [1017] = "form.patient.arbeitgeber",
        [1018] = "form.patient.versicherungstraeger",
        [1019] = "form.patient.familienstand",
        [1020] = "form.assessment_primary.naca",
        [1021] = "form.assessment_primary.atemweg",
        [1022] = "form.assessment_primary.atmung",
        [1023] = "form.assessment_primary.kreislauf",
        [1024] = "form.assessment_primary.bewusstsein",
        [1025] = "form.assessment_primary.angen_notfallzeit.zeit",
        [1026] = "form.assessment_primary.angen_notfallzeit.gt24h",
        [1027] = "form.assessment_primary.angen_notfallzeit.unbekannt",
        [1028] = "form.assessment_secondary.anamnese_text",
        [1029] = "form.assessment_secondary.bodymap",
        [1030] = "form.medications_administered",
        [1031] = "form.vitals.pupillen.R",
        [1032] = "form.vitals.pupillen.L",
        [1033] = "form.vitals.schmerz",
        [1034] = "form.vitals.schmerz_nicht_beurteilbar",
        [1035] = "form.vitals.gcs_augenoeffnen",
        [1036] = "form.vitals.gcs_verbale_reaktion",
        [1037] = "form.vitals.gcs_motorische_reaktion",
        [1038] = "form.vitals.gcs_summe",
        [1039] = "form.vitals.keine",
        [1040] = "form.vitals.rr",
        [1041] = "form.vitals.puls",
        [1042] = "form.vitals.puls_rhythmus",
        [1043] = "form.vitals.af",
        [1044] = "form.vitals.temp",
        [1045] = "form.vitals.bz",
        [1046] = "form.vitals.etco2",
        [1047] = "form.vitals.spo2",
        [1048] = "form.vitals.o2_l_min",
        [1049] = "form.vitals.o2_beatmung_l_min",
        [1050] = "form.measures.herz_kreislauf.massnahmen",
        [1051] = "form.measures.herz_kreislauf.dnr",
        [1052] = "form.measures.herz_kreislauf.anzahl",
        [1053] = "form.measures.herz_kreislauf.letzte_joule",
        [1054] = "form.measures.herz_kreislauf.freq",
        [1055] = "form.measures.herz_kreislauf.mv",
        [1056] = "form.measures.atmung.massnahmen",
        [1057] = "form.measures.atmung.dnr",
        [1058] = "form.measures.atmung.af",
        [1059] = "form.measures.atmung.amv",
        [1060] = "form.measures.atmung.peep",
        [1061] = "form.measures.weitere_massnahmen.massnahmen",
        [1062] = "form.measures.weitere_massnahmen.abbinden_zeit",
        [1063] = "form.measures.weitere_massnahmen.lagerung_art",
        [1064] = "form.history.allergien",
        [1065] = "form.history.medikamente",
        [1066] = "form.history.patientengeschichte_vorerkrankungen",
        [1067] = "form.history.letzte_orale_aufnahme",
        [1068] = "form.history.ereignisse_zuvor",
        [1069] = "form.history.risikofaktoren",
        [1070] = "form.disposition.abschlussart",
        [1071] = "form.disposition.klinischer_zustand",
        [1072] = "form.disposition.uhrzeit_ende",
        [1073] = "form.disposition.org",
        [1074] = "form.disposition.typ",
        [1075] = "form.disposition.kennung",
        [1076] = "form.disposition.angehoerige_in_kenntnis",
        [1077] = "form.disposition.kontaktdaten",
        [1078] = "form.signatures.entlass_san_na",
    };

    public static readonly IReadOnlyDictionary<string, int> Ids = Names.ToDictionary(p => p.Value, p => p.Key);

    // Objects are patched recursively; arrays are one field and are replaced explicitly.
    public static IEnumerable<(string Path, JsonNode? Value)> Flatten(JsonObject value, string prefix = "")
    {
        foreach (var property in value)
        {
            var path = prefix.Length == 0 ? property.Key : $"{prefix}.{property.Key}";
            if (property.Value is JsonObject child)
            {
                foreach (var leaf in Flatten(child, path)) yield return leaf;
            }
            else yield return (path, property.Value);
        }
    }

    public static JsonNode? Get(JsonObject state, string path)
    {
        JsonNode? value = state;
        foreach (var segment in path.Split('.')) value = value?[segment];
        return value;
    }

    public static void Set(JsonObject state, string path, JsonNode? value)
    {
        var segments = path.Split('.');
        var current = state;
        foreach (var segment in segments[..^1]) current = current[segment]!.AsObject();
        current[segments[^1]] = value?.DeepClone();
    }
}
