namespace WayniBcra.Domain;

/// <summary>
/// Represents a single file import process and its lifecycle.
/// Encapsulates the valid state transitions (Pending -> Processing -> Completed/Failed)
/// so that invalid transitions cannot be expressed from outside the domain.
/// </summary>
public sealed class ImportJob
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; }
    public ImportStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    // Summary, populated on successful completion.
    public int ProcessedDebtors { get; private set; }
    public int ProcessedEntities { get; private set; }

    // Error detail, populated on failure.
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Duration between start and finish, once both are known.
    /// </summary>
    public TimeSpan? Duration =>
        StartedAt is not null && FinishedAt is not null
            ? FinishedAt - StartedAt
            : null;

    private ImportJob(Guid id, string fileName, ImportStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        FileName = fileName;
        Status = status;
        CreatedAt = createdAt;
    }

    // Parameterless constructor for EF Core materialization.
    private ImportJob()
    {
        FileName = string.Empty;
    }

    /// <summary>
    /// Creates a new import job in the Pending state.
    /// </summary>
    public static ImportJob Create(string fileName, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new ImportJob(Guid.NewGuid(), fileName, ImportStatus.Pending, now);
    }

    /// <summary>
    /// Transitions the job to Processing. Only valid from Pending.
    /// </summary>
    public void Start(TimeProvider? timeProvider = null)
    {
        EnsureStatus(ImportStatus.Pending, nameof(Start));
        Status = ImportStatus.Processing;
        StartedAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
    }

    /// <summary>
    /// Transitions the job to Completed with its summary. Only valid from Processing.
    /// </summary>
    public void Complete(int processedDebtors, int processedEntities, TimeProvider? timeProvider = null)
    {
        EnsureStatus(ImportStatus.Processing, nameof(Complete));

        if (processedDebtors < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processedDebtors));
        }

        if (processedEntities < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processedEntities));
        }

        Status = ImportStatus.Completed;
        ProcessedDebtors = processedDebtors;
        ProcessedEntities = processedEntities;
        FinishedAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
    }

    /// <summary>
    /// Transitions the job to Failed with an error message. Only valid from Processing.
    /// </summary>
    public void Fail(string errorMessage, TimeProvider? timeProvider = null)
    {
        EnsureStatus(ImportStatus.Processing, nameof(Fail));

        Status = ImportStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error." : errorMessage;
        FinishedAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
    }

    private void EnsureStatus(ImportStatus expected, string operation)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} an import job in status '{Status}'. Expected '{expected}'.");
        }
    }
}
