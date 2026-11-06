using System.Net.Mail;
using Microsoft.Extensions.Options;
using WayniBcra.Application.Notifications;

namespace WayniBcra.Infrastructure.Notifications;

/// <summary>
/// SMTP implementation of <see cref="IEmailSender"/> using the framework's SmtpClient. Targets
/// Mailpit locally (plain SMTP, no auth/TLS); the same code would point at a real SMTP server in
/// production via configuration. Retry/backoff is handled by the dispatcher (outbox), not here.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(to);

        using var client = new SmtpClient(_options.Host, _options.Port);
        await client.SendMailAsync(message, cancellationToken);
    }
}
