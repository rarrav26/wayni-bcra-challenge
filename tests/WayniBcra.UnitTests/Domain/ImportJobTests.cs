using Microsoft.Extensions.Time.Testing;
using WayniBcra.Domain;

namespace WayniBcra.UnitTests.Domain;

public class ImportJobTests
{
    [Fact]
    public void Create_NewJob_StartsInPendingStatus()
    {
        var job = ImportJob.Create("deudores.txt");

        Assert.Equal(ImportStatus.Pending, job.Status);
        Assert.Equal("deudores.txt", job.FileName);
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Null(job.StartedAt);
        Assert.Null(job.FinishedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_InvalidFileName_Throws(string? fileName)
    {
        Assert.Throws<ArgumentException>(() => ImportJob.Create(fileName!));
    }

    [Fact]
    public void Start_FromPending_TransitionsToProcessing()
    {
        var time = new FakeTimeProvider();
        var job = ImportJob.Create("deudores.txt", time);

        job.Start(time);

        Assert.Equal(ImportStatus.Processing, job.Status);
        Assert.Equal(time.GetUtcNow(), job.StartedAt);
    }

    [Fact]
    public void Complete_FromProcessing_TransitionsToCompletedWithSummary()
    {
        var time = new FakeTimeProvider();
        var job = ImportJob.Create("deudores.txt", time);
        job.Start(time);

        time.Advance(TimeSpan.FromSeconds(42));
        job.Complete(processedDebtors: 100, processedEntities: 5, time);

        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(100, job.ProcessedDebtors);
        Assert.Equal(5, job.ProcessedEntities);
        Assert.Equal(TimeSpan.FromSeconds(42), job.Duration);
    }

    [Fact]
    public void Fail_FromProcessing_TransitionsToFailedWithMessage()
    {
        var time = new FakeTimeProvider();
        var job = ImportJob.Create("deudores.txt", time);
        job.Start(time);

        job.Fail("boom", time);

        Assert.Equal(ImportStatus.Failed, job.Status);
        Assert.Equal("boom", job.ErrorMessage);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public void Start_WhenNotPending_Throws()
    {
        var job = ImportJob.Create("deudores.txt");
        job.Start();

        Assert.Throws<InvalidOperationException>(() => job.Start());
    }

    [Fact]
    public void Complete_WhenNotProcessing_Throws()
    {
        var job = ImportJob.Create("deudores.txt");

        Assert.Throws<InvalidOperationException>(() => job.Complete(1, 1));
    }

    [Fact]
    public void Fail_WhenNotProcessing_Throws()
    {
        var job = ImportJob.Create("deudores.txt");

        Assert.Throws<InvalidOperationException>(() => job.Fail("error"));
    }

    [Fact]
    public void Complete_NegativeCounts_Throws()
    {
        var job = ImportJob.Create("deudores.txt");
        job.Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => job.Complete(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => job.Complete(0, -1));
    }

    [Fact]
    public void Duration_BeforeFinish_IsNull()
    {
        var job = ImportJob.Create("deudores.txt");
        Assert.Null(job.Duration);

        job.Start();
        Assert.Null(job.Duration);
    }
}
