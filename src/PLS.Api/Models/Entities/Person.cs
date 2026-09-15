using NetTopologySuite.Geometries;

namespace PLS.Api.Models.Entities;

public class Person
{
    public int Id { get; set; }
    public string? Triagefarbe { get; set; }
    public Point? Position { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
