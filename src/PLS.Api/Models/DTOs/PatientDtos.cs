using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PLS.Api.Models.DTOs;

// ── v2 requests (clean contracts) ─────────────────────────────────────────────
// operationSceneId is an integer ID — no client-side JSON stringification needed.
// respiration and blutung are booleans rather than strings.
public record PersonsV2Request(int operationSceneId);
public record VerifyPatientQrCodeV2Request(string qr_code, int operationSceneId);
public class UpdateTriageColorV2Request : ChangeMetadata
{
    public string? triageColor { get; set; }
    public bool? respiration { get; set; }
    public bool? blutung { get; set; }
}
public class UpdateRespirationV2Request : ChangeMetadata
{
    [Required] public bool? respiration { get; set; }
}
public class UpdatePatientLocationV2Request : ChangeMetadata
{
    [Required, Range(-90.0, 90.0)] public double? lat { get; set; }
    [Required, Range(-180.0, 180.0)] public double? lng { get; set; }
}

public class AmbulanzprotokollPage1UpsertRequest : ChangeMetadata
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "draft";

    [JsonPropertyName("formState")]
    public JsonElement FormState { get; set; }

    // Legacy clients may still use the document revision. Timestamp-aware clients
    // resolve individual fields through ChangedAt/FieldChangedAt instead.
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

    [JsonPropertyName("rejectedChanges")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RejectedChange>? RejectedChanges { get; init; }

    [JsonPropertyName("revision")]
    public int Revision { get; init; }
}
