using System.Globalization;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using WayniBcra.Application.Notifications;
using WayniBcra.Domain;

namespace WayniBcra.Worker;

/// <summary>
/// Background service that delivers pending outbox notifications (Transactional Outbox consumer).
/// Polls the outbox, claims due messages with row locking, and sends each one by email.
///
/// Two complementary layers of resilience:
///  - In-process (Polly): retries a transient send failure a few times with short backoff within
///    the same attempt.
///  - Deferred (outbox): if the send still fails, the message is rescheduled (next_attempt_at,
///    exponential backoff) and retried on a later poll — this covers a prolonged channel outage,
///    which a purely in-process retry cannot. After max attempts the message becomes Failed.
///
/// Runs in the same project/process as <see cref="ImportMessageConsumer"/> as a separate
/// BackgroundService (single responsibility per class, shared host by pragmatism).
/// </summary>
public sealed class NotificationDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationDispatcherOptions _options;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly ResiliencePipeline _sendPipeline;

    public NotificationDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationDispatcherOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        // In-process retry for transient send failures (short exponential backoff).
        _sendPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.InProcessRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Notification dispatcher started. Polling every {PollSeconds}s.", _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatched = await DispatchBatchAsync(stoppingToken);

                // If nothing was dispatched, wait before polling again. If there was work, loop
                // immediately to drain any remaining backlog.
                if (dispatched == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Safety net: never let an unexpected error tear down the loop.
                _logger.LogError(ex, "Unexpected error in the dispatcher loop. Continuing after a short delay.");
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var smtp = scope.ServiceProvider.GetRequiredService<IOptions<Infrastructure.Notifications.SmtpOptions>>().Value;

        var messages = await outbox.ClaimPendingAsync(_options.BatchSize, cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            await DeliverAsync(message, outbox, emailSender, smtp.ToAddress, cancellationToken);
        }

        return messages.Count;
    }

    private async Task DeliverAsync(
        OutboxMessage message,
        IOutboxRepository outbox,
        IEmailSender emailSender,
        string toAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = ImportCompletedPayload.FromJson(message.Payload);
            var (subject, body) = BuildEmail(payload);

            await _sendPipeline.ExecuteAsync(
                async ct => await emailSender.SendAsync(toAddress, subject, body, ct),
                cancellationToken);

            message.MarkSent();
            await outbox.UpdateAsync(message, cancellationToken);
            _logger.LogInformation(
                "Sent notification for import {ImportJobId} (outbox {OutboxId}).",
                message.ImportJobId, message.Id);
        }
        catch (Exception ex)
        {
            message.MarkFailed(
                ex.Message,
                _options.MaxAttempts,
                TimeSpan.FromSeconds(_options.RetryBaseDelaySeconds));
            await outbox.UpdateAsync(message, cancellationToken);

            if (message.Status == OutboxStatus.Failed)
            {
                _logger.LogError(ex,
                    "Notification for import {ImportJobId} failed permanently after {Attempts} attempts (outbox {OutboxId}).",
                    message.ImportJobId, message.Attempts, message.Id);
            }
            else
            {
                _logger.LogWarning(ex,
                    "Notification for import {ImportJobId} failed (attempt {Attempts}); will retry at {NextAttempt} (outbox {OutboxId}).",
                    message.ImportJobId, message.Attempts, message.NextAttemptAt, message.Id);
            }
        }
    }

    private static (string Subject, string Body) BuildEmail(ImportCompletedPayload payload)
    {
        var culture = CultureInfo.GetCultureInfo("es-AR");
        var subject = $"Importación completada — {payload.FileName}";
        var duration = payload.DurationSeconds is { } s
            ? FormatDuration(TimeSpan.FromSeconds(s))
            : "n/d";
        var body =
            $"""
            La importación del padrón de deudores finalizó correctamente.

            Archivo: {payload.FileName}
            Import Job ID: {payload.ImportJobId}
            Deudores procesados: {payload.Debtors.ToString("N0", culture)}
            Entidades procesadas: {payload.Entities.ToString("N0", culture)}
            Duración: {duration}
            Finalizada: {payload.CompletedAt:yyyy-MM-dd HH:mm:ss 'UTC'}
            """;
        return (subject, body);
    }

    // Human-readable duration, e.g. "10 min 21 s" or "1 h 05 min 09 s".
    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalHours >= 1)
        {
            return $"{(int)d.TotalHours} h {d.Minutes:00} min {d.Seconds:00} s";
        }
        if (d.TotalMinutes >= 1)
        {
            return $"{d.Minutes} min {d.Seconds:00} s";
        }
        return $"{d.Seconds} s";
    }
}
