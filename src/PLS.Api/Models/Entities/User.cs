namespace PLS.Api.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool AdminRole { get; set; }
    public bool RequiresPasswordChange { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public double? LongitudeUser { get; set; }
    public double? LatitudeUser { get; set; }
    public DateTime? FirstLoginTime { get; set; }
    public DateTime? LastLoginTime { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
