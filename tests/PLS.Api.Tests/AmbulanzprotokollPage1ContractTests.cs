using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using PLS.Api.Models;

namespace PLS.Api.Tests;

[Collection("Integration")]
public class AmbulanzprotokollPage1ContractTests
{
    private readonly HttpClient _client;

    public AmbulanzprotokollPage1ContractTests(PlsWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public void CanonicalSpec_IsValidSingleJsonDocument()
    {
        using var document = LoadCanonicalSpecDocument();

        Assert.True(document.RootElement.TryGetProperty("meta", out _));
        Assert.True(document.RootElement.TryGetProperty("document", out _));
        Assert.True(document.RootElement.TryGetProperty("zones", out _));
        Assert.True(document.RootElement.TryGetProperty("stateSchema", out _));
    }

    [Fact]
    public void EveryBindPath_ExistsInCanonicalStateSchema()
    {
        using var document = LoadCanonicalSpecDocument();
        var stateSchema = document.RootElement.GetProperty("stateSchema");
        var bindPaths = CollectBindPaths(document.RootElement.GetProperty("zones"));

        foreach (var bindPath in bindPaths)
        {
            Assert.True(
                TryGetPath(stateSchema, bindPath, out _),
                $"Bind path '{bindPath}' is declared in zones but missing from stateSchema.");
        }
    }

    [Fact]
    public void BackendDefaults_MatchCanonicalStateSchema()
    {
        var specStateSchema = JsonNode.Parse(LoadCanonicalSpecText())!["stateSchema"];
        var backendDefaults = JsonNode.Parse(AmbulanzprotokollPage1Defaults.DefaultFormState);

        Assert.NotNull(specStateSchema);
        Assert.NotNull(backendDefaults);
        Assert.True(
            JsonNode.DeepEquals(specStateSchema, backendDefaults),
            "Backend defaults diverge from docs/specs/ambulanzprotokoll_page1.json stateSchema.");
    }

    [Fact]
    public async Task BoundValues_SurvivePutGetRoundTrip_Unchanged()
    {
        using var document = LoadCanonicalSpecDocument();
        var bindPaths = CollectBindPaths(document.RootElement.GetProperty("zones"));
        var sampleStateNode = BuildSampleState(JsonNode.Parse(document.RootElement.GetProperty("stateSchema").GetRawText())!);
        var sampleState = JsonDocument.Parse(sampleStateNode.ToJsonString()).RootElement.Clone();

        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        await PutFormStateAsync(token, patientId, "draft", sampleStateNode);

        var returnedState = await GetFormStateAsync(token, patientId);

        foreach (var bindPath in bindPaths)
        {
            Assert.True(TryGetPath(sampleState, bindPath, out var expected), $"Sample state is missing '{bindPath}'.");
            Assert.True(TryGetPath(returnedState, bindPath, out var actual), $"Returned state is missing '{bindPath}'.");
            Assert.True(
                JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonNode.Parse(actual.GetRawText())),
                $"Round-trip mismatch at bind path '{bindPath}'. Expected {expected.GetRawText()} but got {actual.GetRawText()}.");
        }
    }

    private static HashSet<string> CollectBindPaths(JsonElement element)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        CollectBindPathsRecursive(element, paths);
        return paths;
    }

    private static void CollectBindPathsRecursive(JsonElement element, HashSet<string> paths)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.NameEquals("bind") || property.NameEquals("naBind"))
                        && property.Value.ValueKind == JsonValueKind.String)
                    {
                        paths.Add(property.Value.GetString()!);
                    }

                    CollectBindPathsRecursive(property.Value, paths);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectBindPathsRecursive(item, paths);
                break;
        }
    }

    private static bool TryGetPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return false;
        }

        return true;
    }

    private static JsonNode BuildSampleState(JsonNode node, string path = "")
    {
        switch (node)
        {
            case JsonObject obj:
                if (path == "assessment_primary.angen_notfallzeit")
                {
                    return new JsonObject
                    {
                        ["zeit"] = "08:15",
                        ["gt24h"] = true,
                        ["unbekannt"] = false
                    };
                }

                if (path == "vitals.pupillen")
                {
                    return new JsonObject
                    {
                        ["R"] = new JsonArray("eng", "prompte Lichtreflexe"),
                        ["L"] = new JsonArray("mittel", "verlangsamte Lichtreflexe")
                    };
                }

                var resultObject = new JsonObject();
                foreach (var property in obj)
                {
                    var childPath = string.IsNullOrEmpty(path) ? property.Key : $"{path}.{property.Key}";
                    resultObject[property.Key] = property.Value is null
                        ? BuildSampleForNullLikeValue(childPath)
                        : BuildSampleState(property.Value, childPath);
                }

                return resultObject;

            case JsonArray:
                return path switch
                {
                    "assessment_secondary.bodymap" => new JsonArray(
                        new JsonObject
                        {
                            ["view"] = "front",
                            ["marker"] = "schmerz",
                            ["x"] = 12.5,
                            ["y"] = 24.75
                        },
                        new JsonObject
                        {
                            ["view"] = "back",
                            ["marker"] = "wunde",
                            ["x"] = 61.0,
                            ["y"] = 48.5
                        }),
                    "medications_administered" => new JsonArray(
                        new JsonObject
                        {
                            ["medikament"] = "NaCl",
                            ["dosis"] = "500 ml",
                            ["art"] = "i.v.",
                            ["uhrzeit"] = "08:35"
                        }),
                    _ => new JsonArray("sample")
                };

            case JsonValue value:
                return BuildSampleScalar(value, path);

            default:
                return node.DeepClone();
        }
    }

    private static JsonNode BuildSampleScalar(JsonValue value, string path)
    {
        if (path == "signatures.entlass_san_na")
            return JsonValue.Create("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB")!;

        return value.TryGetValue<bool>(out _)
            ? JsonValue.Create(true)!
            : value.TryGetValue<string>(out var stringValue)
                ? JsonValue.Create(SampleStringValue(path, stringValue))!
                : BuildSampleForNullLikeValue(path);
    }

    private static JsonNode BuildSampleForNullLikeValue(string path)
    {
        return path switch
        {
            "incident.datum" => JsonValue.Create("2026-03-31")!,
            "incident.uhrzeit_beginn" => JsonValue.Create("08:00")!,
            "patient.geschlecht" => JsonValue.Create("w")!,
            "patient.geburtsdatum" => JsonValue.Create("1980-01-02")!,
            "vitals.schmerz" => JsonValue.Create(7)!,
            "vitals.gcs_augenoeffnen" => JsonValue.Create(4)!,
            "vitals.gcs_verbale_reaktion" => JsonValue.Create(5)!,
            "vitals.gcs_motorische_reaktion" => JsonValue.Create(6)!,
            "vitals.gcs_summe" => JsonValue.Create(15)!,
            "measures.weitere_massnahmen.abbinden_zeit" => JsonValue.Create("09:05")!,
            "disposition.uhrzeit_ende" => JsonValue.Create("10:20")!,
            "signatures.entlass_san_na" => JsonValue.Create("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB")!,
            _ => JsonValue.Create("sample")!
        };
    }

    private static string SampleStringValue(string path, string current)
    {
        if (path.EndsWith("datum", StringComparison.Ordinal) || path.EndsWith("geburtsdatum", StringComparison.Ordinal))
            return "2026-03-31";

        if (path.Contains("uhrzeit", StringComparison.Ordinal) || path.EndsWith(".zeit", StringComparison.Ordinal))
            return "08:15";

        if (path == "vitals.rr")
            return "120/80";

        if (path == "patient.telefon")
            return "+431234567";

        if (current.Length == 0)
            return $"sample:{path}";

        return current;
    }

    private static string LoadCanonicalSpecText()
    {
        return File.ReadAllText(GetCanonicalSpecPath());
    }

    private static JsonDocument LoadCanonicalSpecDocument()
    {
        return JsonDocument.Parse(LoadCanonicalSpecText());
    }

    private static string GetCanonicalSpecPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "specs", "ambulanzprotokoll_page1.json");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate docs/specs/ambulanzprotokoll_page1.json from test output directory.");
    }

    private async Task<JsonElement> GetFormStateAsync(string token, int patientId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return payload!["formState"];
    }

    private async Task PutFormStateAsync(string token, int patientId, string status, JsonNode formState)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1")
        {
            Content = JsonContent.Create(new
            {
                status,
                formState
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["token"].GetString()!;
    }

    private async Task<int> CreatePatientAsync(string token)
    {
        var sceneId = await CreateSceneAsync(token);
        var qrCode = await GeneratePatientQrCodeAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["patientId"].GetInt32();
    }

    private async Task<int> CreateSceneAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = $"Contract {Guid.NewGuid():N}" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!["idoperationScene"].GetInt32();
    }

    private async Task<string> GeneratePatientQrCodeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["qrcodes"][0].GetString()!;
    }
}
