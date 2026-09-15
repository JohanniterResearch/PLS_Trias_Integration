using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;

namespace PLS.Api.Controllers;

[ApiController]
public class QRLoginController : ControllerBase
{
    private readonly PlsDbContext _context;
    private static readonly char[] Characters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    public QRLoginController(PlsDbContext context)
    {
        _context = context;
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
    [HttpPost("api/generateLoginQRCodes")]
    public async Task<IActionResult> GenerateLoginQRCodes([FromBody] GenerateQrCodesRequest request)
    {
        var qrCodes = new List<string>();

        for (int i = 0; i < request.number; i++)
        {
            string tempQR;
            do
            {
                tempQR = GenerateRandomQR(64);
            } while (await _context.QrCodeLogins.AnyAsync(q => q.QrLogin == tempQR));

            _context.QrCodeLogins.Add(new QrCodeLogin { QrLogin = tempQR });
            qrCodes.Add(tempQR);
        }

        await _context.SaveChangesAsync();
        return Ok(new { status = "success", qrcodes = qrCodes });
    }

    /// <summary>Returns login QR codes that have not been consumed yet.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("api/getLoginQrCodes")]
    public async Task<IActionResult> GetLoginQrCodes()
    {
        var codes = await _context.QrCodeLogins
            .Where(q => q.FirstLogin == null)
            .Select(q => q.QrLogin)
            .ToListAsync();

        return Ok(codes);
    }
}
