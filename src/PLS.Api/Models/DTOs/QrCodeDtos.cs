using System.ComponentModel.DataAnnotations;

namespace PLS.Api.Models.DTOs;

// Bulk QR generation is capped: each generated code costs a uniqueness query, so an
// unbounded count is a trivial resource-exhaustion vector (H-07).
public record GenerateQrCodesRequest([Range(1, 100)] int number);
