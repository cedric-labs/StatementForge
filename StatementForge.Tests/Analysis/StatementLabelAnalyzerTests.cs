using AwesomeAssertions;
using StatementForge.Analysis;
using Xunit;

namespace StatementForge.Tests.Analysis;

public sealed class StatementLabelAnalyzerTests
{
    [Theory]
    [InlineData("PRLV SEPA NETFLIX.COM 12345678", null, "NETFLIX.COM")]
    [InlineData("PRLV SEPA AXA ASSURANCES", "MANDAT 987654 CONTRAT AUTO", "AXA AUTO")]
    [InlineData("PRLV SEPA AXA ASSURANCES", "MANDAT 987654 CONTRAT HABITATION", "AXA HABITATION")]
    [InlineData("CB FREE TELECOM 12/04/2026", null, "FREE TELECOM")]
    [InlineData("   ", null, "")]
    public void CleanTransactionLabel_WhenNoisyBankLabelProvided_ExtractsNormalizedIssuerAndContract(
        string label,
        string? rawLabel,
        string expected)
    {
        StatementLabelAnalyzer.CleanTransactionLabel(label, rawLabel).Should().Be(expected);
    }

    [Fact]
    public void FormatSuggestedName_WhenUppercasePatternProvided_FormatsAsTitleCase()
    {
        StatementLabelAnalyzer.FormatSuggestedName("AXA AUTO").Should().Be("Axa Auto");
        StatementLabelAnalyzer.FormatSuggestedName("   ").Should().BeEmpty();
    }

    [Theory]
    [InlineData("PRLV NETFLIX", null, "netflix", true)]
    [InlineData("PRLV", "NETFLIX RAW", "netflix", true)]
    [InlineData("PRLV EDF", "FACTURE ELEC 2026", "EDF*ELEC*", true)]
    [InlineData("VIR LOYER AVRIL", null, "^VIR\\s+LOYER", true)]
    [InlineData("CARREFOUR CITY", null, "NETFLIX", false)]
    [InlineData("CARREFOUR CITY", null, "", false)]
    [InlineData("CARREFOUR CITY", null, "[invalid-regex", false)]
    public void MatchesPattern_WhenSubstringWildcardOrRegexProvided_ReturnsExpectedMatchResult(
        string label,
        string? rawLabel,
        string pattern,
        bool expected)
    {
        StatementLabelAnalyzer.MatchesPattern(label, rawLabel, pattern).Should().Be(expected);
    }

    [Fact]
    public void MatchesAmount_WhenWithinOrOutsideTolerance_ReturnsExpectedBoolean()
    {
        StatementLabelAnalyzer.MatchesAmount(100m, 0.05m, 104m).Should().BeTrue();
        StatementLabelAnalyzer.MatchesAmount(100m, 0.05m, 110m).Should().BeFalse();
        StatementLabelAnalyzer.MatchesAmount(100m, 0m, 100.005m).Should().BeTrue();
        StatementLabelAnalyzer.MatchesAmount(0m, 0.05m, 250m).Should().BeTrue();
    }

    [Fact]
    public void ExtractMeaningfulTokens_WhenGenericWordsAndDatesPresent_ExtractsOnlyMerchantTokens()
    {
        var tokens = StatementLabelAnalyzer.ExtractMeaningfulTokens(
            "PAIEMENT PAR CARTE X0305 24/09 BOULANGERIE PAUL",
            "OPERATIONS WEB SEPA");

        tokens.Should().Contain("boulangerie").And.Contain("paul");
        tokens.Should().NotContain("paiement").And.NotContain("carte").And.NotContain("x0305");

        StatementLabelAnalyzer.ExtractMeaningfulTokens("   ", null).Should().BeEmpty();
    }

    [Fact]
    public void IsLabelMatching_WhenComparingSimilarTransactions_ReturnsTrueOnTokenOverlap()
    {
        var tokens = StatementLabelAnalyzer.ExtractMeaningfulTokens("CB CARREFOUR MARKET 12/05", null);

        StatementLabelAnalyzer.IsLabelMatching(tokens, "PAIEMENT CARTE CARREFOUR", null, sameDate: false)
            .Should().BeTrue();
        StatementLabelAnalyzer.IsLabelMatching(tokens, "PRLV EDF", null, sameDate: true)
            .Should().BeFalse();

        var emptyTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        StatementLabelAnalyzer.IsTokenSetMatching(emptyTokens, emptyTokens, sameDate: true).Should().BeTrue();
        StatementLabelAnalyzer.IsTokenSetMatching(emptyTokens, emptyTokens, sameDate: false).Should().BeFalse();
    }

    [Fact]
    public void DetectCadence_WhenMonthlyQuarterlyOrAnnualDatesProvided_DetectsExpectedFrequency()
    {
        DateTime[] monthly =
        [
            new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc)
        ];

        var (detectedM, freqM, regM) = RecurringCadenceAnalyzer.DetectCadence(monthly);
        detectedM.Should().BeTrue();
        freqM.Should().Be(StatementRecurrenceFrequency.Monthly);
        regM.Should().BeTrue();

        DateTime[] quarterly =
        [
            new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc)
        ];
        var (detectedQ, freqQ, regQ) = RecurringCadenceAnalyzer.DetectCadence(quarterly);
        detectedQ.Should().BeTrue();
        freqQ.Should().Be(StatementRecurrenceFrequency.Quarterly);
        regQ.Should().BeTrue();

        DateTime[] annual =
        [
            new(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)
        ];
        var (detectedA, freqA, _) = RecurringCadenceAnalyzer.DetectCadence(annual);
        detectedA.Should().BeTrue();
        freqA.Should().Be(StatementRecurrenceFrequency.Annual);

        DateTime[] irregular =
        [
            new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc)
        ];
        RecurringCadenceAnalyzer.DetectCadence(irregular).IsDetected.Should().BeFalse();
        RecurringCadenceAnalyzer.DetectCadence([new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)]).IsDetected.Should().BeFalse();

        RecurringCadenceAnalyzer.CalculateConfidenceScore(5, 29.99m, 29.99m).Should().Be(98);
        RecurringCadenceAnalyzer.AppliesToMonth(StatementRecurrenceFrequency.Monthly, 5).Should().BeTrue();
        RecurringCadenceAnalyzer.AppliesToMonth(StatementRecurrenceFrequency.Quarterly, 3).Should().BeTrue();
        RecurringCadenceAnalyzer.AppliesToMonth(StatementRecurrenceFrequency.Biannual, 6).Should().BeTrue();
        RecurringCadenceAnalyzer.AppliesToMonth(StatementRecurrenceFrequency.Annual, 12).Should().BeTrue();
    }
}
