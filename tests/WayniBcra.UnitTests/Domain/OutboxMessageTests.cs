using Microsoft.Extensions.Time.Testing;
using WayniBcra.Domain;

namespace WayniBcra.UnitTests.Domain;

public class OutboxMessageTests
{
    private const string Payload = """{"importJobId":"x"}""";

    [Fact]
    public void Create_NewMessage_StartsPendingAndDispatchableNow()
    {
        var time = new FakeTimeProvider();
        var importJobId = Guid.NewGuid();

        var message = OutboxMessage.Create(importJobId, NotificationType.Email, Payload, time);

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(importJobId, message.ImportJobId);
        Assert.Equal(NotificationType.Email, message.NotificationType);
        Assert.Equal(Payload, message.Payload);
        Assert.Equal(0, message.Attempts);
        Assert.Equal(time.GetUtcNow(), message.NextAttemptAt);
        Assert.NotEqual(Guid.Empty, message.Id);
    }

    [Fact]
    public void Create_EmptyImportJobId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => OutboxMessage.Create(Guid.Empty, NotificationType.Email, Payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_InvalidPayload_Throws(string? payload)
    {
        Assert.Throws<ArgumentException>(
            () => OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, payload!));
    }

    [Fact]
    public void Reserve_FromPending_TransitionsToProcessingAndIncrementsAttempts()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload);

        message.Reserve();

        Assert.Equal(OutboxStatus.Processing, message.Status);
        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public void Reserve_WhenNotPending_Throws()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload);
        message.Reserve();

        Assert.Throws<InvalidOperationException>(() => message.Reserve());
    }

    [Fact]
    public void MarkSent_FromProcessing_TransitionsToSent()
    {
        var time = new FakeTimeProvider();
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload, time);
        message.Reserve();

        message.MarkSent(time);

        Assert.Equal(OutboxStatus.Sent, message.Status);
        Assert.Equal(time.GetUtcNow(), message.SentAt);
        Assert.Null(message.LastError);
    }

    [Fact]
    public void MarkSent_WhenNotProcessing_Throws()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload);

        Assert.Throws<InvalidOperationException>(() => message.MarkSent());
    }

    [Fact]
    public void MarkFailed_WithAttemptsRemaining_GoesBackToPendingWithBackoff()
    {
        var time = new FakeTimeProvider();
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload, time);
        message.Reserve(); // attempts = 1

        var baseDelay = TimeSpan.FromSeconds(30);
        message.MarkFailed("smtp down", maxAttempts: 5, baseRetryDelay: baseDelay, timeProvider: time);

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal("smtp down", message.LastError);
        // First retry backoff: baseDelay * 2^(attempts-1) = 30s * 2^0 = 30s.
        Assert.Equal(time.GetUtcNow() + baseDelay, message.NextAttemptAt);
    }

    [Fact]
    public void MarkFailed_AppliesExponentialBackoff()
    {
        var time = new FakeTimeProvider();
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload, time);
        var baseDelay = TimeSpan.FromSeconds(30);

        // Second attempt: backoff should be 30s * 2^(2-1) = 60s.
        message.Reserve();
        message.MarkFailed("fail 1", maxAttempts: 5, baseRetryDelay: baseDelay, timeProvider: time);
        message.Reserve(); // attempts = 2
        message.MarkFailed("fail 2", maxAttempts: 5, baseRetryDelay: baseDelay, timeProvider: time);

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromSeconds(60), message.NextAttemptAt);
    }

    [Fact]
    public void MarkFailed_WhenMaxAttemptsReached_TransitionsToFailed()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload);

        // maxAttempts = 1: the first (and only) attempt fails -> terminal Failed.
        message.Reserve(); // attempts = 1
        message.MarkFailed("boom", maxAttempts: 1, baseRetryDelay: TimeSpan.FromSeconds(30));

        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Equal("boom", message.LastError);
    }

    [Fact]
    public void MarkFailed_WhenNotProcessing_Throws()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationType.Email, Payload);

        Assert.Throws<InvalidOperationException>(
            () => message.MarkFailed("x", maxAttempts: 3, baseRetryDelay: TimeSpan.FromSeconds(1)));
    }
}
