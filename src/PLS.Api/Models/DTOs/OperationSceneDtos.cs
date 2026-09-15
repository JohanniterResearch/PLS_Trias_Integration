using System.ComponentModel.DataAnnotations;

namespace PLS.Api.Models.DTOs;

public record CreateOperationSceneRequest(int? id, [Required, MaxLength(255)] string name, string? description);

public record SelectSceneRequest([Range(1, int.MaxValue)] int operationSceneId);
