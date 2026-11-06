using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WayniBcra.Application.Imports;
using WayniBcra.Application.Notifications;
using WayniBcra.Domain;

namespace WayniBcra.UnitTests.Imports;

public class ProcessImportHandlerTests
{
    private readonly IImportJobRepository _importJobRepository = Substitute.For<IImportJobRepository>();
    private readonly IImportRecordReader _recordReader = Substitute.For<IImportRecordReader>();
    private readonly IImportProcessor _importProcessor = Substitute.For<IImportProcessor>();
    private readonly IOutboxRepository _outboxRepository = Substitute.For<IOutboxRepository>();

    private ProcessImportHandler CreateHandler() => new(
        _importJobRepository,
        _recordReader,
        _importProcessor,
        _outboxRepository,
        NullLogger<ProcessImportHandler>.Instance);

    private static async IAsyncEnumerable<BcraRecord> EmptyRecords()
    {
        await Task.CompletedTask;
        yield break;
    }

    [Fact]
    public async Task HandleAsync_OnSuccess_StagesOutboxMessageForTheImport()
    {
        var job = ImportJob.Create("deudores.txt");
        _importJobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _recordReader.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(EmptyRecords());
        _importProcessor.ProcessAsync(job.Id, Arg.Any<IAsyncEnumerable<BcraRecord>>(), Arg.Any<CancellationToken>())
            .Returns(new ImportResult { ProcessedRecords = 10, AggregatedDebtors = 8, AggregatedEntities = 2 });

        OutboxMessage? staged = null;
        await _outboxRepository.AddAsync(Arg.Do<OutboxMessage>(m => staged = m), Arg.Any<CancellationToken>());

        var handler = CreateHandler();
        await handler.HandleAsync(job.Id, Stream.Null);

        // An outbox message was staged for this import, as an email notification.
        Assert.NotNull(staged);
        Assert.Equal(job.Id, staged!.ImportJobId);
        Assert.Equal(NotificationType.Email, staged.NotificationType);
        Assert.Contains(job.Id.ToString(), staged.Payload);
        Assert.Equal(ImportStatus.Completed, job.Status);
    }

    [Fact]
    public async Task HandleAsync_OnSuccess_StagesOutboxBeforeTheCompletionSaveChanges()
    {
        // The outbox insert must happen before the SaveChanges that commits the completion, so both
        // are persisted together (atomic intent to notify). The job is already Completed by the time
        // the outbox is staged; we assert that ordering, ignoring the earlier Start() SaveChanges.
        var job = ImportJob.Create("deudores.txt");
        _importJobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _recordReader.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(EmptyRecords());
        _importProcessor.ProcessAsync(job.Id, Arg.Any<IAsyncEnumerable<BcraRecord>>(), Arg.Any<CancellationToken>())
            .Returns(new ImportResult { ProcessedRecords = 1, AggregatedDebtors = 1, AggregatedEntities = 1 });

        var saveChangesCountWhenStaged = 0;
        var saveChangesCount = 0;
        _importJobRepository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { saveChangesCount++; return Task.CompletedTask; });
        await _outboxRepository.AddAsync(
            Arg.Do<OutboxMessage>(_ => saveChangesCountWhenStaged = saveChangesCount),
            Arg.Any<CancellationToken>());

        var handler = CreateHandler();
        await handler.HandleAsync(job.Id, Stream.Null);

        // The job is Completed, and the outbox was staged after the Start() SaveChanges (count == 1)
        // but before the completion SaveChanges (which brings the total to 2). So exactly one
        // SaveChanges had run when the outbox was staged, and the completion SaveChanges follows it.
        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(1, saveChangesCountWhenStaged);
        Assert.Equal(2, saveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingFails_DoesNotStageOutboxAndRethrows()
    {
        var job = ImportJob.Create("deudores.txt");
        _importJobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        _recordReader.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(EmptyRecords());
        _importProcessor.ProcessAsync(job.Id, Arg.Any<IAsyncEnumerable<BcraRecord>>(), Arg.Any<CancellationToken>())
            .Returns<ImportResult>(_ => throw new InvalidOperationException("boom"));

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(job.Id, Stream.Null));

        // No notification should be staged for a failed import.
        await _outboxRepository.DidNotReceive().AddAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        Assert.Equal(ImportStatus.Failed, job.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenJobNotFound_Throws()
    {
        var missingId = Guid.NewGuid();
        _importJobRepository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(missingId, Stream.Null));
    }
}
