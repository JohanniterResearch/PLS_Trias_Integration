namespace PLS.Api.Models.Entities;

public class Patient
{
    public int Id { get; set; }
    public bool? Atmung { get; set; }
    public bool? Blutung { get; set; }
    public bool? Radialispuls { get; set; }
    public string? Triagefarbe { get; set; }
    public bool? Transport { get; set; }
    public bool? Dringend { get; set; }
    public bool? Kontaminiert { get; set; }
    public string? Name { get; set; }
    public double? LongitudePatient { get; set; }
    public double? LatitudePatient { get; set; }
    public int? UserIdUser { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }
    public Body? Body { get; set; }
    public AmbulanzprotokollPage1? AmbulanzprotokollPage1 { get; set; }
    public QrCodePatient? QrCodePatient { get; set; }
}
