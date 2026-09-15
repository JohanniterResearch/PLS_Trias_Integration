namespace PLS.Api.Models.Entities;

public class QrCodeLoginHasUser
{
    public int QrCodeLoginId { get; set; }
    public int UserId { get; set; }

    public QrCodeLogin? QrCodeLogin { get; set; }
    public User? User { get; set; }
}
