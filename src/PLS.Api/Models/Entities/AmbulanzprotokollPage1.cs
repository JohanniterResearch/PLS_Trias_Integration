namespace PLS.Api.Models.Entities;

public class AmbulanzprotokollPage1
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string FormStateJson { get; set; } = "{}";
    public string Status { get; set; } = "draft";
    // Server-managed revision for optimistic concurrency: each accepted write
    // increments it; clients send the revision they based their edit on.
    public int Revision { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Patient? Patient { get; set; }
}
