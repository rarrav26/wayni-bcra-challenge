using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WayniBcra.Application.Imports;

namespace WayniBcra.Infrastructure.Imports;

/// <summary>
/// Streams BCRA records from a stream, parsing each line with <see cref="BcraRecordParser"/>.
/// Reads line by line without loading the whole file into memory.
/// Fault-tolerant: lines that fail to parse are logged (structured) and skipped, so a few
/// corrupt lines do not abort the whole import. Invalid-line counting is performed by the
/// ingestion service that consumes this stream (it compares total vs yielded records).
/// </summary>
public sealed class BcraImportRecordReader : IImportRecordReader
{
    private readonly ILogger<BcraImportRecordReader> _logger;

    public BcraImportRecordReader(ILogger<BcraImportRecordReader> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<BcraRecord> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);

        long lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++;

            if (line.Length == 0)
            {
                continue;
            }

            BcraRecord? record = null;
            try
            {
                record = BcraRecordParser.ParseLine(line);
            }
            catch (FormatException ex)
            {
                // Structured logging: LineNumber and Reason are indexable fields (CloudWatch/Seq/etc.).
                // We do not log the line content to avoid leaking PII.
                _logger.LogWarning(
                    "Skipping invalid BCRA line {LineNumber}: {Reason}", lineNumber, ex.Message);
            }

            if (record is not null)
            {
                yield return record;
            }
        }
    }
}
