using System.ComponentModel.DataAnnotations;

namespace PLS.Api.Models.DTOs;

public class SaveBodyPartRequest : ChangeMetadata
{
    [Required] public string bodyPartId { get; set; } = "";
    [Required, Range(0, 1)] public int? isClicked { get; set; }
    public int idpatient { get; set; }
}
