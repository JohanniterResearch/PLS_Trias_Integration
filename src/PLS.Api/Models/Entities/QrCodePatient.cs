namespace PLS.Api.Models.Entities;

public class QrCodePatient
{
    public int Id { get; set; }
    public string QrLogin { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public int? UserId { get; set; }
    public int? OperationSceneId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Patient? Patient { get; set; }
    public User? User { get; set; }
    public OperationScene? OperationScene { get; set; }
}
