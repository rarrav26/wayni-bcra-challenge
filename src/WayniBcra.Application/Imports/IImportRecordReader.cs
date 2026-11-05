namespace WayniBcra.Application.Imports;

/// <summary>
/// Reads BCRA records from a stream by streaming, yielding one <see cref="BcraRecord"/> at a time
/// without loading the whole file into memory. The concrete implementation (in Infrastructure)
/// uses the fixed-width parser internally. The source of the stream (disk, S3, network) is not a
/// concern of this contract.
/// </summary>
public interface IImportRecordReader
{
    /// <summary>
    /// Reads the records from the given stream as an asynchronous sequence.
    /// </summary>
    /// <param name="stream">The input stream containing the BCRA fixed-width file.</param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>An asynchronous sequence of parsed records.</returns>
    IAsyncEnumerable<BcraRecord> ReadAsync(Stream stream, CancellationToken cancellationToken = default);
}
