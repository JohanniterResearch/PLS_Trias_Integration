namespace PLS.Api.Models.Entities;

public class PatientChange
{
    public long Id { get; set; }
    public int PatientId { get; set; }
    public int FieldId { get; set; }
    public string Value { get; set; } = "null";
    public DateTime ChangedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool Accepted { get; set; }
    public string Reason { get; set; } = "accepted";
    public string? ActorId { get; set; }
    public string? DeviceId { get; set; }
}
