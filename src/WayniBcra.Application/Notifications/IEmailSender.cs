namespace WayniBcra.Application.Notifications;

/// <summary>
/// Sends an email. Implemented in Infrastructure (SMTP). The notification dispatcher uses it to
/// deliver import-completion notifications; the outbox is channel-agnostic, so a webhook sender
/// would be an alternative adapter behind a similar port.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email. Throws on delivery failure so the caller can record a failed attempt and retry.
    /// </summary>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
