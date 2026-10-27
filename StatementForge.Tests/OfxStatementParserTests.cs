using System.Text;
using AwesomeAssertions;
using StatementForge.Models;
using StatementForge.Parsing;
using Xunit;

namespace StatementForge.Tests;

public class OfxStatementParserTests
{
    private readonly OfxStatementParser _parser = new();

    [Theory]
    [InlineData("statement.ofx", true)]
    [InlineData("statement.OFX", true)]
    [InlineData("bank.qfx", true)]
    [InlineData("data.csv", false)]
    [InlineData("report.pdf", false)]
    public void CanHandle_WhenCheckingFileExtension_ReturnsExpectedResult(string fileName, bool expected)
    {
        using var stream = new MemoryStream();
        var result = _parser.CanHandle(fileName, stream);
        result.Should().Be(expected);
    }

    [Fact]
    public void CanHandle_WhenInspectingOfxContentHeader_DetectsFormatSuccessfully()
    {
        var content = "OFXHEADER:100\r\nDATA:OFXSGML\r\n<OFX>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = _parser.CanHandle("unknown.txt", stream);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractOfxAccountIdAsync_WhenValidTagPresent_ReturnsAccountId()
    {
        var ofxContent = @"
OFXHEADER:100
<OFX>
<BANKMSGSRSV1>
<STMTTRNRS>
<STMTRS>
<BANKACCTFROM>
<BANKID>30004
<BRANCHID>02837
<ACCTID> 00012345678A </ACCTID>
<ACCTTYPE>CHECKING
</BANKACCTFROM>
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ofxContent));

        var accountId = await OfxStatementParser.ExtractOfxAccountIdAsync(stream);

        accountId.Should().Be("00012345678A");
    }

    [Fact]
    public async Task ExtractOfxAccountIdAsync_WhenTagMissing_ReturnsNull()
    {
        var ofxContent = @"
OFXHEADER:100
<OFX>
<BANKMSGSRSV1>
<STMTTRNRS>
<STMTRS>
<BANKACCTFROM>
<BANKID>30004
</BANKACCTFROM>
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ofxContent));

        var accountId = await OfxStatementParser.ExtractOfxAccountIdAsync(stream);

        accountId.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_WhenStandardOfxGiven_ParsesTransactionsAccurately()
    {
        var ofxContent = @"
OFXHEADER:100
DATA:OFXSGML
<OFX>
<BANKMSGSRSV1>
<STMTTRNRS>
<STMTRS>
<BANKTRANLIST>
<DTSTART>20261001
<DTEND>20261015
<STMTTRN>
<TRNTYPE>DEBIT
<DTPOSTED>20261010120000[0:GMT]
<TRNAMT>-45.50
<FITID>TX10001
<NAME>SUPERMARCHE BIO
<MEMO>Courses hebdomadaires
</STMTTRN>
<STMTTRN>
<TRNTYPE>CREDIT
<DTPOSTED>20261012
<TRNAMT>2500.00
<FITID>TX10002
<NAME>VIREMENT SALAIRE
<MEMO>Revenu - Salaire Octobre
</STMTTRN>
</BANKTRANLIST>
</STMTRS>
</STMTTRNRS>
</BANKMSGSRSV1>
</OFX>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ofxContent));

        var results = await _parser.ParseAsync(stream);

        results.Should().HaveCount(2);

        var tx1 = results[0];
        tx1.Date.Should().Be(new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));
        tx1.Amount.Should().Be(45.50m);
        tx1.Type.Should().Be(StatementTransactionType.Debit);
        tx1.Label.Should().Be("SUPERMARCHE BIO");
        tx1.Reference.Should().Be("TX10001");
        tx1.ExternalId.Should().Be("TX10001");

        var tx2 = results[1];
        tx2.Date.Should().Be(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));
        tx2.Amount.Should().Be(2500.00m);
        tx2.Type.Should().Be(StatementTransactionType.Credit);
        tx2.Label.Should().Be("VIREMENT SALAIRE");
        tx2.CategoryCandidate.Should().Be("Revenu");
    }

    [Fact]
    public async Task ParseAsync_WhenStreamIsEmpty_ReturnsEmptyList()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());

        var results = await _parser.ParseAsync(stream);

        results.Should().BeEmpty();
    }
}
