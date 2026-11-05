using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using WayniBcra.Application.Imports;
using WayniBcra.Infrastructure.Persistence;

namespace WayniBcra.Infrastructure.Imports;

/// <summary>
/// Ingests records into the staging table using PostgreSQL COPY (binary), then aggregates them
/// into the final tables (debtors, financial_entities) within a single transaction (ADR-019).
/// Idempotent per import via staging cleanup + upsert.
/// </summary>
public sealed class NpgsqlImportProcessor : IImportProcessor
{
    private readonly WayniBcraDbContext _dbContext;
    private readonly ILogger<NpgsqlImportProcessor> _logger;

    public NpgsqlImportProcessor(WayniBcraDbContext dbContext, ILogger<NpgsqlImportProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ImportResult> ProcessAsync(
        Guid importJobId,
        IAsyncEnumerable<BcraRecord> records,
        CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Clean any previous staging rows for this import (idempotent reprocessing).
            await DeleteStagingAsync(connection, transaction, importJobId, cancellationToken);

            // 2. COPY the records into staging (binary).
            var processedRecords = await CopyToStagingAsync(connection, importJobId, records, cancellationToken);

            // 3. Aggregate into the final tables (upsert).
            var aggregatedDebtors = await AggregateDebtorsAsync(connection, transaction, importJobId, cancellationToken);
            var aggregatedEntities = await AggregateEntitiesAsync(connection, transaction, importJobId, cancellationToken);

            // 4. Clean the staging rows for this import.
            await DeleteStagingAsync(connection, transaction, importJobId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Import {ImportJobId} processed: {Records} records -> {Debtors} debtors, {Entities} entities.",
                importJobId, processedRecords, aggregatedDebtors, aggregatedEntities);

            return new ImportResult
            {
                ProcessedRecords = processedRecords,
                AggregatedDebtors = aggregatedDebtors,
                AggregatedEntities = aggregatedEntities
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task DeleteStagingAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid importJobId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM staging_records WHERE import_job_id = @importJobId", connection, transaction);
        cmd.Parameters.AddWithValue("importJobId", importJobId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> CopyToStagingAsync(
        NpgsqlConnection connection, Guid importJobId, IAsyncEnumerable<BcraRecord> records, CancellationToken ct)
    {
        const string copyCommand =
            "COPY staging_records (import_job_id, entity_code, identification_type, identification_number, credit_status, financing_amount) " +
            "FROM STDIN (FORMAT BINARY)";

        long count = 0;
        await using var writer = await connection.BeginBinaryImportAsync(copyCommand, ct);

        await foreach (var record in records.WithCancellation(ct))
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(importJobId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(record.EntityCode, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(record.IdentificationType, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(record.IdentificationNumber, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(record.CreditStatus, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(record.FinancingAmount, NpgsqlDbType.Numeric, ct);
            count++;
        }

        await writer.CompleteAsync(ct);
        return count;
    }

    // Debtors: aggregate by identification_number. Sum the financing amount, and pick the credit status
    // of highest credit severity via the ranking (ADR-014). The original BCRA code is stored, never the
    // ranking value. Unknown codes get priority -1 (lowest) so they never win as "worst" (ADR-019).
    private static async Task<int> AggregateDebtorsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid importJobId, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO debtors (identification_number, max_credit_status, total_financing_amount)
SELECT
    agg.identification_number,
    sev.max_credit_status,
    agg.total_financing_amount
FROM (
    SELECT identification_number, SUM(financing_amount) AS total_financing_amount
    FROM staging_records
    WHERE import_job_id = @importJobId
    GROUP BY identification_number
) AS agg
JOIN (
    SELECT DISTINCT ON (identification_number)
        identification_number,
        credit_status AS max_credit_status
    FROM staging_records
    WHERE import_job_id = @importJobId
    ORDER BY identification_number,
        CASE credit_status
            WHEN '11' THEN 0
            WHEN '01' THEN 1
            WHEN '21' THEN 2
            WHEN '23' THEN 3
            WHEN '03' THEN 4
            WHEN '04' THEN 5
            WHEN '05' THEN 6
            ELSE -1
        END DESC
) AS sev ON sev.identification_number = agg.identification_number
ON CONFLICT (identification_number) DO UPDATE SET
    max_credit_status = EXCLUDED.max_credit_status,
    total_financing_amount = EXCLUDED.total_financing_amount;";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("importJobId", importJobId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // Financial entities: aggregate the financing amount by entity_code.
    private static async Task<int> AggregateEntitiesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid importJobId, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO financial_entities (entity_code, total_financing_amount)
SELECT entity_code, SUM(financing_amount)
FROM staging_records
WHERE import_job_id = @importJobId
GROUP BY entity_code
ON CONFLICT (entity_code) DO UPDATE SET
    total_financing_amount = EXCLUDED.total_financing_amount;";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("importJobId", importJobId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}
