using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace FitMind.Services;

public interface IEmailSender
{
    Task<bool> SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SenderEmail { get; set; } = "";
    public string SenderName { get; set; } = "FitMind";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = "";
    public string SenderEmail { get; set; } = "onboarding@resend.dev";
    public string SenderName { get; set; } = "FitMind";
}

public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var resend = options.Value;
        if (string.IsNullOrWhiteSpace(resend.ApiKey))
        {
            logger.LogError("Resend is selected, but its API key is missing.");
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{resend.SenderName} <{resend.SenderEmail}>",
                to = new[] { recipient },
                subject,
                html = htmlBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Resend rejected an email request with status {StatusCode}: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(exception, "Resend failed to send an email.");
            return false;
        }
    }
}

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;
        if (!smtp.Enabled)
        {
            logger.LogWarning("SMTP disabled; password reset email for {Recipient} was not sent.", recipient);
            return false;
        }

        if (string.IsNullOrWhiteSpace(smtp.Host)
            || string.IsNullOrWhiteSpace(smtp.SenderEmail)
            || string.IsNullOrWhiteSpace(smtp.Username)
            || string.IsNullOrWhiteSpace(smtp.Password))
        {
            logger.LogError("SMTP is enabled, but required settings are missing.");
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.SenderEmail, smtp.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipient));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            Credentials = new NetworkCredential(smtp.Username, smtp.Password)
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogError(exception, "SMTP failed to send password reset email.");
            return false;
        }
    }
}
