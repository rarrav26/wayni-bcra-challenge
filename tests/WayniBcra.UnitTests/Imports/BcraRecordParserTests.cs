using WayniBcra.Infrastructure.Imports;

namespace WayniBcra.UnitTests.Imports;

public class BcraRecordParserTests
{
    // Builds a valid 171-char line from the fields we care about, padding the rest with zeros/spaces.
    // Layout: [0:5] entity, [5:11] date, [11:13] idType, [13:24] idNumber, [24:27] activity,
    // [27:29] status, [29:41] amount, [41:171] remaining fields (filled to reach width 171).
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

        var head = entity + date + idType + idNumber + activity + status + amount; // 41 chars
        var line = head.PadRight(BcraRecordParser.LineWidth, '0');
        return line;
    }

    [Fact]
    public void ParseLine_ValidLine_ExtractsAllFields()
    {
        var line = BuildLine(
            entityCode: "00007",
            identificationType: "11",
            identificationNumber: "20123456789",
            creditStatus: "05",
            financingAmount: "123,4");

        var record = BcraRecordParser.ParseLine(line);

        Assert.Equal("00007", record.EntityCode);
        Assert.Equal("11", record.IdentificationType);
        Assert.Equal("20123456789", record.IdentificationNumber);
        Assert.Equal("05", record.CreditStatus);
        Assert.Equal(123.4m, record.FinancingAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(170)]
    [InlineData(172)]
    [InlineData(100)]
    public void ParseLine_InvalidWidth_Throws(int width)
    {
        var line = new string('0', width);
        Assert.Throws<FormatException>(() => BcraRecordParser.ParseLine(line));
    }

    [Theory]
    [InlineData("1,0", 1.0)]
    [InlineData(",0", 0.0)]
    [InlineData("0,0", 0.0)]
    [InlineData("123,4", 123.4)]
    [InlineData("99999999999,9", 99999999999.9)]
    [InlineData("  1,0 ", 1.0)]
    public void ParseFinancingAmount_ValidFormats_Parses(string raw, decimal expected)
    {
        var padded = raw.PadLeft(12, ' ');
        var result = BcraRecordParser.ParseFinancingAmount(padded);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("            ")] // all spaces -> empty after trim
    [InlineData("abc,0       ")] // non-numeric
    public void ParseFinancingAmount_InvalidFormats_Throws(string raw)
    {
        Assert.Throws<FormatException>(() => BcraRecordParser.ParseFinancingAmount(raw));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("04")]
    [InlineData("05")]
    [InlineData("11")]
    [InlineData("21")]
    [InlineData("23")]
    public void ParseLine_KnownCreditStatuses_PreservedAsTwoDigitString(string status)
    {
        var line = BuildLine(creditStatus: status);
        var record = BcraRecordParser.ParseLine(line);
        Assert.Equal(status, record.CreditStatus);
    }

    [Theory]
    [InlineData("11")] // CUIT/CUIL/CDI
    [InlineData("98")] // other type seen in the real file
    [InlineData("99")] // other type seen in the real file
    public void ParseLine_IdentificationTypes_Preserved(string idType)
    {
        var line = BuildLine(identificationType: idType);
        var record = BcraRecordParser.ParseLine(line);
        Assert.Equal(idType, record.IdentificationType);
    }

    [Fact]
    public void ParseLine_EntityCodeWithLeadingZeros_Preserved()
    {
        var line = BuildLine(entityCode: "00011");
        var record = BcraRecordParser.ParseLine(line);
        Assert.Equal("00011", record.EntityCode);
    }

    [Fact]
    public void ParseLine_ZeroAmount_Parses()
    {
        var line = BuildLine(financingAmount: ",0");
        var record = BcraRecordParser.ParseLine(line);
        Assert.Equal(0m, record.FinancingAmount);
    }
}
