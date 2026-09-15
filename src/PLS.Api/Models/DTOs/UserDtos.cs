using System.ComponentModel.DataAnnotations;

namespace PLS.Api.Models.DTOs;

public record CreateAdminUserRequest(
    [Required, MinLength(3), MaxLength(64)] string username,
    [Required, MinLength(8), MaxLength(128)] string password);

public record ChangeAdminPasswordRequest(
    [Required, MaxLength(64)] string username,
    [Required] string password,
    [Required, MinLength(8), MaxLength(128)] string newpassword);
