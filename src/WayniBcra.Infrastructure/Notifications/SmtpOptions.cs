namespace WayniBcra.Infrastructure.Notifications;

/// <summary>
/// SMTP configuration (bound from the "Smtp" section). For local development/evaluation this points
/// at Mailpit, which captures the emails in its web UI.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;

    /// <summary>Sender address used in the "From" header.</summary>
    public string FromAddress { get; set; } = "no-reply@wayni-bcra.local";
    public string FromName { get; set; } = "Wayni BCRA";

    /// <summary>Recipient of the import-completion notifications.</summary>
    public string ToAddress { get; set; } = "ops@wayni-bcra.local";
}
