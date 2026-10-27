using System.Text;
using AwesomeAssertions;
using StatementForge.Models;
using StatementForge.Parsing;
using Xunit;

namespace StatementForge.Tests;

public class CsvStatementParserTests
{
    private readonly CsvStatementParser _parser = new();

    [Theory]
    [InlineData("statement.csv", true)]
    [InlineData("statement.tsv", true)]
    [InlineData("statement.txt", true)]
    [InlineData("statement.ofx", false)]
    [InlineData("statement.json", false)]
    public void CanHandle_WhenCheckingFileExtension_ReturnsExpectedResult(string fileName, bool expected)
    {
        using var stream = new MemoryStream();
        var result = _parser.CanHandle(fileName, stream);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ParseAsync_WhenStandardCsvWithSingleAmountColumn_ParsesCorrectly()
    {
        var csv = @"Date;Libellé;Montant;Référence
10/10/2026;Paiement Carte Boulangerie;-12,80;REF001
12/10/2026;Virement Reçu;+150,00;REF002
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var options = new CsvStatementOptions
        {
            Delimiter = ";",
            DecimalSeparator = ",",
            DateFormat = "dd/MM/yyyy",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = 2,
            ReferenceColumnIndex = 3
        };

        var results = await _parser.ParseAsync(stream, options);

        results.Should().HaveCount(2);

        var tx1 = results[0];
        tx1.Date.Should().Be(new DateTime(2026, 10, 10));
        tx1.Amount.Should().Be(12.80m);
        tx1.Type.Should().Be(StatementTransactionType.Debit);
        tx1.Label.Should().Be("Paiement Carte Boulangerie");
        tx1.Reference.Should().Be("REF001");

        var tx2 = results[1];
        tx2.Date.Should().Be(new DateTime(2026, 10, 12));
        tx2.Amount.Should().Be(150.00m);
        tx2.Type.Should().Be(StatementTransactionType.Credit);
        tx2.Label.Should().Be("Virement Reçu");
        tx2.Reference.Should().Be("REF002");
    }

    [Fact]
    public async Task ParseAsync_WhenSeparateDebitAndCreditColumns_ParsesTypesAndAmountsProperly()
    {
        var csv = @"Date,Label,Debit,Credit
2026-10-10,Restaurant,45.00,
2026-10-15,Remboursement,,30.50
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var options = new CsvStatementOptions
        {
            Delimiter = ",",
            DecimalSeparator = ".",
            DateFormat = "yyyy-MM-dd",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = null,
            DebitColumnIndex = 2,
            CreditColumnIndex = 3
        };

        var results = await _parser.ParseAsync(stream, options);

        results.Should().HaveCount(2);

        results[0].Amount.Should().Be(45.00m);
        results[0].Type.Should().Be(StatementTransactionType.Debit);

        results[1].Amount.Should().Be(30.50m);
        results[1].Type.Should().Be(StatementTransactionType.Credit);
    }

    [Fact]
    public async Task ParseAsync_WhenFieldsHaveQuotesAndSpaces_CleansCorrectly()
    {
        var csv = @"""Date"";""Libellé"";""Montant""
""10/10/2026"";""  Paiement   Par   Carte  "";""-25,00 €""
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var options = new CsvStatementOptions
        {
            Delimiter = ";",
            DecimalSeparator = ",",
            DateFormat = "dd/MM/yyyy",
            HasHeader = true,
            DateColumnIndex = 0,
            LabelColumnIndex = 1,
            AmountColumnIndex = 2
        };

        var results = await _parser.ParseAsync(stream, options);

        results.Should().HaveCount(1);
        results[0].Label.Should().Be("Paiement Par Carte");
        results[0].Amount.Should().Be(25.00m);
        results[0].Type.Should().Be(StatementTransactionType.Debit);
    }
}
