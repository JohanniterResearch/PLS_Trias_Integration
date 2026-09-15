using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;

namespace PLS.Api.Controllers;

[ApiController]
public class PatientQrCodeController : ControllerBase
{
    private readonly PlsDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private static readonly char[] Characters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    public PatientQrCodeController(
        PlsDbContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
    }

    private static string GenerateRandomQR(int length = 64)
    {
        return string.Create(length, (object?)null, (span, _) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = Characters[RandomNumberGenerator.GetInt32(Characters.Length)];
        });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/generatePatientQRCodes")]
    public async Task<IActionResult> GeneratePatientQRCodes([FromBody] GenerateQrCodesRequest request)
    {
        var qrCodes = new List<string>();

        for (int i = 0; i < request.number; i++)
        {
            string tempQR;
            do
            {
                tempQR = GenerateRandomQR(64);
            } while (await _context.QrCodePatients.AnyAsync(q => q.QrLogin == tempQR));

            _context.QrCodePatients.Add(new QrCodePatient { QrLogin = tempQR });
            qrCodes.Add(tempQR);
        }

        await _context.SaveChangesAsync();
        return Ok(new { status = "success", qrcodes = qrCodes });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("api/getUnusedPatientQrCodes")]
    public async Task<IActionResult> GetUnusedPatientQrCodes()
    {
        var codes = await _context.QrCodePatients
            .Where(q => q.PatientId == null)
            .Select(q => q.QrLogin)
            .ToListAsync();

        return Ok(codes);
    }

    // DEV WORKAROUND — remove before production
    [Authorize(Policy = "QrOrAdmin")]
    [HttpGet("api/dev/unusedPatientQrCodes")]
    public async Task<IActionResult> GetUnusedPatientQrCodesDev()
    {
        // Explicit opt-in only, and never outside development.
        var isEnabled = _environment.IsDevelopment()
                        && _configuration.GetValue<bool>("Features:EnableDevQrHelper");
        if (!isEnabled)
            return NotFound();

        var unused = await _context.QrCodePatients
            .Where(q => q.PatientId == null)
            .Select(q => q.QrLogin)
            .ToListAsync();
        return Ok(unused);
    }
}
