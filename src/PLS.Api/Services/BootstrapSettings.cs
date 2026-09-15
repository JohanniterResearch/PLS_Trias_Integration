namespace PLS.Api.Services;

public class BootstrapSettings
{
    public string AdminUsername { get; set; } = "admin";
    public string? AdminPassword { get; set; }
    public string TestUserUsername { get; set; } = "user";
    public string TestUserPassword { get; set; } = "password";
    public bool SeedDevSampleData { get; set; } = false;
}
