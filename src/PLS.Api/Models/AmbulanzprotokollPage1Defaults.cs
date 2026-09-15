using System.Text.Json;
using System.Text.Json.Nodes;

namespace PLS.Api.Models;

public static class AmbulanzprotokollPage1Defaults
{
    private const string DefaultFormStateJson = """
    {
      "incident": {
        "ambulanzort": "",
        "datum": null,
        "uhrzeit_beginn": null,
        "dnr_san_1": "",
        "dnr_san_2": "",
        "dnr_san_3": "",
        "dnr_na": "",
        "pls_nummer": "",
        "funkrufname": ""
      },
      "patient": {
        "familienname": "",
        "vorname": "",
        "geschlecht": null,
        "vers_nr": "",
        "geburtsdatum": null,
        "adresse": "",
        "staat": "",
        "telefon": "",
        "arbeitgeber": "",
        "versicherungstraeger": "",
        "familienstand": ""
      },
      "assessment_primary": {
        "naca": [],
        "atemweg": [],
        "atmung": [],
        "kreislauf": [],
        "bewusstsein": [],
        "angen_notfallzeit": {
          "zeit": null,
          "gt24h": false,
          "unbekannt": false
        }
      },
      "assessment_secondary": {
        "anamnese_text": "",
        "bodymap": []
      },
      "medications_administered": [],
      "vitals": {
        "pupillen": { "R": [], "L": [] },
        "schmerz": null,
        "schmerz_nicht_beurteilbar": false,
        "gcs_augenoeffnen": null,
        "gcs_verbale_reaktion": null,
        "gcs_motorische_reaktion": null,
        "gcs_summe": null,
        "keine": false,
        "rr": "",
        "puls": "",
        "puls_rhythmus": [],
        "af": "",
        "temp": "",
        "bz": "",
        "etco2": "",
        "spo2": "",
        "o2_l_min": "",
        "o2_beatmung_l_min": ""
      },
      "measures": {
        "herz_kreislauf": {
          "massnahmen": [],
          "dnr": "",
          "anzahl": "",
          "letzte_joule": "",
          "freq": "",
          "mv": ""
        },
        "atmung": {
          "massnahmen": [],
          "dnr": "",
          "af": "",
          "amv": "",
          "peep": ""
        },
        "weitere_massnahmen": {
          "massnahmen": [],
          "abbinden_zeit": null,
          "lagerung_art": ""
        }
      },
      "history": {
        "allergien": "",
        "medikamente": "",
        "patientengeschichte_vorerkrankungen": "",
        "letzte_orale_aufnahme": "",
        "ereignisse_zuvor": "",
        "risikofaktoren": ""
      },
      "disposition": {
        "abschlussart": [],
        "klinischer_zustand": [],
        "uhrzeit_ende": null,
        "org": "",
        "typ": "",
        "kennung": "",
        "angehoerige_in_kenntnis": [],
        "kontaktdaten": ""
      },
      "signatures": {
        "entlass_san_na": null
      }
    }
    """;

    public static string DefaultFormState => DefaultFormStateJson;

    public static JsonElement CreateDefaultFormState()
    {
        using var document = JsonDocument.Parse(DefaultFormStateJson);
        return document.RootElement.Clone();
    }

    public static string MergeWithDefaultFormState(string? partialJson)
    {
        var baseNode = JsonNode.Parse(DefaultFormStateJson)?.DeepClone()?.AsObject()
            ?? new JsonObject();

        if (string.IsNullOrWhiteSpace(partialJson))
            return baseNode.ToJsonString();

        var incomingNode = JsonNode.Parse(partialJson);
        if (incomingNode is null)
            return baseNode.ToJsonString();

        MergeNodes(baseNode, incomingNode);
        return baseNode.ToJsonString();
    }

    private static void MergeNodes(JsonNode target, JsonNode source)
    {
        if (target is JsonObject targetObject && source is JsonObject sourceObject)
        {
            foreach (var property in sourceObject)
            {
                if (property.Value is null)
                {
                    targetObject[property.Key] = null;
                    continue;
                }

                if (targetObject[property.Key] is JsonObject existingObject && property.Value is JsonObject sourceChild)
                {
                    MergeNodes(existingObject, sourceChild);
                    continue;
                }

                targetObject[property.Key] = property.Value.DeepClone();
            }

            return;
        }

        if (target is JsonArray targetArray && source is JsonArray sourceArray)
        {
            targetArray.Clear();
            foreach (var item in sourceArray)
                targetArray.Add(item?.DeepClone());
        }
    }
}
