using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LAVAL.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LAVAL.Web.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly EmailSettings _emailSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ContactController> _logger;

    public ContactController(
        IOptions<EmailSettings> emailSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<ContactController> logger)
    {
        _emailSettings = emailSettings.Value;
        _httpClientFactory = httpClientFactory;
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

        var subject = $"Nueva consulta web - {request.FullName}";
        var body = $"""
                    Nueva consulta desde el formulario web.

                    Nombre: {request.FullName}
                    Teléfono / WhatsApp: {request.Phone}
                    Email: {request.Email}

                    Mensaje:
                    {request.Message}
                    """;

        if (!string.IsNullOrWhiteSpace(_emailSettings.ResendApiKey))
        {
            try
            {
                await SendWithResendAsync(replyToAddress!.Address, subject, body);
                return Ok(new { success = true, message = "Consulta enviada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send contact email using Resend API.");
                return StatusCode(503, new
                {
                    success = false,
                    message = "El servidor de correo no responde en este momento. Intentá nuevamente en unos minutos."
                });
            }
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.Password))
        {
            _logger.LogError("Email settings are missing. Configure EmailSettings__ResendApiKey or EmailSettings__Password.");
            return StatusCode(500, new
            {
                success = false,
                message = "Falta configurar el envío de mail en el servidor."
            });
        }

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
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                Timeout = 15000
            };

            await smtp.SendMailAsync(mail);

            return Ok(new { success = true, message = "Consulta enviada correctamente." });
        }
        catch (SmtpException ex) when (ex.InnerException is SocketException socketEx &&
                                       socketEx.SocketErrorCode is SocketError.TimedOut or SocketError.HostUnreachable or SocketError.NetworkUnreachable)
        {
            _logger.LogError(ex, "SMTP connection failed due to network timeout/unreachable.");
            return StatusCode(503, new
            {
                success = false,
                message = "El servidor de correo no responde en este momento. Intentá nuevamente en unos minutos."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact email.");
            return StatusCode(500, new { success = false, message = "No pudimos enviar tu consulta en este momento." });
        }
    }

    private async Task SendWithResendAsync(string replyToEmail, string subject, string body)
    {
        var from = string.IsNullOrWhiteSpace(_emailSettings.ResendFrom) ? _emailSettings.From : _emailSettings.ResendFrom;
        var to = _emailSettings.To;

        if (!TryCreateMailAddress(from, out _))
        {
            throw new InvalidOperationException("Invalid Resend sender address. Configure EmailSettings__ResendFrom or EmailSettings__From.");
        }

        if (!TryCreateMailAddress(to, out _))
        {
            throw new InvalidOperationException("Invalid destination address. Configure EmailSettings__To.");
        }

        var payload = new
        {
            from,
            to = new[] { to },
            subject,
            text = body,
            reply_to = replyToEmail
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _emailSettings.ResendApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        using var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Resend API request failed ({(int)response.StatusCode}): {responseBody}");
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
