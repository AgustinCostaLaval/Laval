using System.Net;
using System.Net.Mail;
using LAVAL.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LAVAL.Web.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<ContactController> _logger;

    public ContactController(IOptions<EmailSettings> emailSettings, ILogger<ContactController> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] ContactRequest request)
    {
        request.FullName = request.FullName?.Trim() ?? string.Empty;
        request.Phone = request.Phone?.Trim() ?? string.Empty;
        request.Email = request.Email?.Trim() ?? string.Empty;
        request.Message = request.Message?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { success = false, message = "Completá todos los campos obligatorios." });
        }

        if (!TryCreateMailAddress(request.Email, out var replyToAddress))
        {
            return BadRequest(new { success = false, message = "Ingresá un email válido." });
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.Password))
        {
            _logger.LogError("EmailSettings.Password is missing. Configure EmailSettings__Password.");
            return StatusCode(500, new
            {
                success = false,
                message = "Falta configurar el envío de mail en el servidor."
            });
        }

        var subject = $"Nueva consulta web - {request.FullName}";
        var body = $"""
                    Nueva consulta desde el formulario web.

                    Nombre: {request.FullName}
                    Teléfono / WhatsApp: {request.Phone}
                    Email: {request.Email}

                    Mensaje:
                    {request.Message}
                    """;

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_emailSettings.From),
                Subject = subject,
                Body = body
            };

            mail.To.Add(_emailSettings.To);
            mail.ReplyToList.Add(replyToAddress!);

            using var smtp = new SmtpClient(_emailSettings.Host, _emailSettings.Port)
            {
                EnableSsl = _emailSettings.EnableSsl,
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password)
            };

            await smtp.SendMailAsync(mail);

            return Ok(new { success = true, message = "Consulta enviada correctamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact email.");
            return StatusCode(500, new { success = false, message = "No pudimos enviar tu consulta en este momento." });
        }
    }

    private static bool TryCreateMailAddress(string value, out MailAddress? address)
    {
        try
        {
            address = new MailAddress(value);
            return true;
        }
        catch
        {
            address = null;
            return false;
        }
    }
}
