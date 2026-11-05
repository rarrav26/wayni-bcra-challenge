using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using WayniBcra.Infrastructure.Imports;

namespace WayniBcra.UnitTests.Imports;

public class BcraImportRecordReaderTests
{
    // Builds a valid 171-char line with the given field values.
    private static string BuildLine(
        string entityCode = "00007",
        string identificationType = "11",
        string identificationNumber = "20123456789",
        string creditStatus = "01",
        string financingAmount = "1,0")
    {
        var entity = entityCode.PadLeft(5, '0');
        const string date = "202401";
        var idType = identificationType.PadLeft(2, '0');
        var idNumber = identificationNumber.PadRight(11, ' ');
        const string activity = "000";
        var status = creditStatus.PadLeft(2, '0');
        var amount = financingAmount.PadLeft(12, ' ');
        var head = entity + date + idType + idNumber + activity + status + amount;
        return head.PadRight(BcraRecordParser.LineWidth, '0');
    }

    private static Stream StreamOf(params string[] lines)
        => new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

    private static BcraImportRecordReader CreateReader()
        => new(NullLogger<BcraImportRecordReader>.Instance);

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }

    [Fact]
    public async Task ReadAsync_AllValidLines_YieldsAllRecords()
    {
        var stream = StreamOf(BuildLine(creditStatus: "01"), BuildLine(creditStatus: "05"));
        var reader = CreateReader();

        var records = await ToListAsync(reader.ReadAsync(stream));

        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task ReadAsync_InvalidWidthLine_SkipsItAndContinues()
    {
        // A valid line, a too-short line, and another valid line.
        var stream = StreamOf(BuildLine(), "SHORT_LINE", BuildLine());
        var reader = CreateReader();

        var records = await ToListAsync(reader.ReadAsync(stream));

        // The invalid line is skipped; the two valid ones are yielded.
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task ReadAsync_EmptyLines_AreIgnored()
    {
        var stream = StreamOf(BuildLine(), "", BuildLine(), "");
        var reader = CreateReader();

        var records = await ToListAsync(reader.ReadAsync(stream));

        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task ReadAsync_OnlyInvalidLines_YieldsNothing()
    {
        var stream = StreamOf("BAD", "ALSO_BAD");
        var reader = CreateReader();

        var records = await ToListAsync(reader.ReadAsync(stream));

        Assert.Empty(records);
    }
}
