using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PLS.Api.Models.DTOs;

public class ChangeMetadata : IValidatableObject
{
    [JsonPropertyName("changedAt")]
    public DateTimeOffset? ChangedAt { get; set; }
    [JsonPropertyName("deviceId"), MaxLength(128)]
    public string? DeviceId { get; set; }
    [JsonPropertyName("fieldChangedAt")]
    public Dictionary<string, DateTimeOffset>? FieldChangedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Future device times must not block subsequent writes indefinitely.
        var earliest = new DateTimeOffset(1000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        if (ChangedAt < earliest || FieldChangedAt?.Values.Any(t => t < earliest) == true)
            yield return new ValidationResult("Edit time is outside the supported database range.");
        var limit = DateTimeOffset.UtcNow.AddMinutes(5);
        if (ChangedAt > limit || FieldChangedAt?.Values.Any(t => t > limit) == true)
            yield return new ValidationResult("Änderungszeit liegt mehr als fünf Minuten in der Zukunft.");
    }
}

public record RejectedChange(int fieldId, string field, string reason, object? currentValue, DateTime? currentChangedAt);
