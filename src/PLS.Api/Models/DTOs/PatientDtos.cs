using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PLS.Api.Models.DTOs;

// ── v2 requests (clean contracts) ─────────────────────────────────────────────
// operationSceneId is an integer ID — no client-side JSON stringification needed.
// respiration and blutung are booleans rather than strings.
public record PersonsV2Request(int operationSceneId);
public record VerifyPatientQrCodeV2Request(string qr_code, int operationSceneId);
public record UpdateTriageColorV2Request(string? triageColor, bool? respiration, bool? blutung);
public record UpdateRespirationV2Request(bool respiration);
public record UpdatePatientLocationV2Request(
    [Range(-90.0, 90.0)] double lat,
    [Range(-180.0, 180.0)] double lng);

public class AmbulanzprotokollPage1UpsertRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "draft";

    [JsonPropertyName("formState")]
    public JsonElement FormState { get; set; }

    // Optimistic concurrency: the revision this edit is based on. Null skips the
    // check (legacy clients); a mismatch returns 409 with the server's revision.
    [JsonPropertyName("expectedRevision")]
    public int? ExpectedRevision { get; set; }
}

public class AmbulanzprotokollPage1Response
{
    [JsonPropertyName("patientId")]
    public int PatientId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "draft";

    [JsonPropertyName("formState")]
    public JsonElement FormState { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; init; }

    [JsonPropertyName("finalizedAt")]
    public DateTime? FinalizedAt { get; init; }

    [JsonPropertyName("revision")]
    public int Revision { get; init; }
}
