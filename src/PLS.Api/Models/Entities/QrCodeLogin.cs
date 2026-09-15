namespace PLS.Api.Models.Entities;

public class QrCodeLogin
{
    public int Id { get; set; }
    public string QrLogin { get; set; } = string.Empty;
    public DateTime? FirstLogin { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
