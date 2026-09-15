namespace PLS.Api.Models.Entities;

public class Body
{
    public int Id { get; set; }
    public int IdPatient { get; set; }
    public Dictionary<string, int> BodyParts { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Patient? Patient { get; set; }
}
