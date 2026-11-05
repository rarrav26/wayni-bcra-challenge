using WayniBcra.Infrastructure.Imports;

namespace WayniBcra.UnitTests.Imports;

public class BcraRecordParserTests
{
    // Builds a valid 171-char line mimicking the REAL BCRA file layout.
    // Layout: [0:5] entity, [5:11] date, [11:13] idType, [13:24] idNumber, [24:27] activity,
    // [27:29] status, [29:41] amount, [41:171] remaining fields (filled to reach width 171).
    // Important: the real file left-aligns and space-pads the status field ("1 ", "5 "), so the
    // raw status is placed with PadRight to reproduce that format faithfully.
    private static string BuildLine(
        string entityCode = "00007",
        string identificationType = "11",
        string rawCreditStatus = "1 ",
        string identificationNumber = "20123456789",
        string financingAmount = "1,0")
    {
        var entity = entityCode.PadLeft(5, '0');
        const string date = "202401";
        var idType = identificationType.PadLeft(2, '0');
        var idNumber = identificationNumber.PadRight(11, ' ');
        const string activity = "000";
        var status = rawCreditStatus.PadRight(2, ' ')[..2]; // exactly 2 chars as in the file
        var amount = financingAmount.PadLeft(12, ' ');

        var head = entity + date + idType + idNumber + activity + status + amount; // 41 chars
        var line = head.PadRight(BcraRecordParser.LineWidth, '0');
        return line;
    }

    [Fact]
    public void ParseLine_ValidLine_ExtractsAllFields()
    {
        // Status "5 " (left-aligned, space-padded, as in the real file) must normalize to "05".
        var line = BuildLine(
            entityCode: "00007",
            identificationType: "11",
            identificationNumber: "20123456789",
            rawCreditStatus: "5 ",
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

    // The real file left-aligns and space-pads the status: single-digit codes come as "1 ".."5 ",
    // two-digit codes come complete ("11", "21", "23"). All must normalize to a 2-digit zero-padded code.
    [Theory]
    [InlineData("1 ", "01")]
    [InlineData("3 ", "03")]
    [InlineData("4 ", "04")]
    [InlineData("5 ", "05")]
    [InlineData("11", "11")]
    [InlineData("21", "21")]
    [InlineData("23", "23")]
    public void ParseLine_CreditStatus_NormalizedToTwoDigitZeroPadded(string rawStatus, string expected)
    {
        var line = BuildLine(rawCreditStatus: rawStatus);
        var record = BcraRecordParser.ParseLine(line);
        Assert.Equal(expected, record.CreditStatus);
    }

    // Direct tests of the normalization helper, covering the real space-padded format.
    [Theory]
    [InlineData("1 ", "01")]
    [InlineData(" 1", "01")]
    [InlineData("5 ", "05")]
    [InlineData("11", "11")]
    [InlineData("23", "23")]
    public void NormalizeCreditStatus_ReturnsTwoDigitZeroPadded(string raw, string expected)
    {
        Assert.Equal(expected, BcraRecordParser.NormalizeCreditStatus(raw));
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
