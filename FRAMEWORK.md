<p align="right">
  <b>English</b> | <a href="FRAMEWORK.fr.md">Français</a>
</p>

# StatementForge — Framework Specification & AI Documentation

This document is the technical manifest intended for AI agents and software engineers. It outlines all capabilities, architectural components, and public contracts exposed by **StatementForge**.

---

## 🎯 Scope & Responsibility

StatementForge is a shared .NET library dedicated to **banking statement processing, financial format parsing, and transactional algorithmic analysis** (OFX 1.x SGML, OFX 2.x XML, QFX, configurable CSV/TSV, label normalization, fuzzy duplicate detection, recurrence cadences, and sliding budget/payroll cycle calculations). It is 100% domain- and database-agnostic.

---

## 🧱 Components & Detailed Capabilities

### 1. `StatementForge.Parsing` — Financial Statement Parsers
- **`IBankStatementParser`** (interface):
  - `string SupportedFormat`: Identifier of the supported format (`OFX`, `CSV`).
  - `bool CanHandle(string fileName, Stream stream)`: Detection by file extension and non-destructive inspection of the stream header bytes.
  - `Task<IReadOnlyList<StatementTransaction>> ParseAsync(Stream stream, CsvStatementOptions? options, CancellationToken cancellationToken)`: Asynchronous normalized transaction extraction with cooperative cancellation.
- **`OfxStatementParser`**:
  - Full support for OFX 1.x (SGML unclosed tags) and OFX 2.x (standard XML).
  - Parsing of `<STMTTRN>` blocks with anti-ReDoS regex timeout defenses.
  - `ExtractOfxAccountIdAsync(Stream stream, CancellationToken cancellationToken)`: Non-destructive static extraction of the account identifier (`<ACCTID>`).
  - Signed amount normalization (`TRNAMT`) mapped to strictly typed Debit / Credit transaction semantics.
  - Compact banking date parsing (`YYYYMMDD[HHMMSS]`) with timezone offset stripping (`[0:GMT]`).
- **`CsvStatementParser`**:
  - RFC 4180-compliant streaming delimited parsing with quote escaping and customizable delimiters.
  - Dual support for signed single-column amounts or separated Debit / Credit columns.
  - Cultural tolerance for decimal separators (`,`, `.`), thousands groupings, non-breaking spaces, and currency symbols (`€`, `EUR`, `$`).

### 2. `StatementForge.Analysis` — Normalization, Deduplication, Cadence & Cycles
- **`StatementLabelAnalyzer`**:
  - `CleanTransactionLabel(string label, string? rawLabel)`: Strips banking transaction prefixes (`PRLV SEPA`, `VIR`, `CB`, dates, numeric IDs) and isolates contract tokens (`AUTO`, `HABITATION`, `SANTE`, `BOX`, `MOBILE`, etc.).
  - `FormatSuggestedName(string pattern)`: Title Case formatting of normalized merchant/payee entities.
  - `MatchesPattern(string label, string? rawLabel, string pattern)`: Case-insensitive evaluation via substring, wildcards (`*`, `%`), or ReDoS-guarded regular expressions.
  - `MatchesAmount(decimal expectedAmount, decimal tolerancePercentage, decimal transactionAmount)`: Range tolerance validation for variable subscription charges.
  - `ExtractMeaningfulTokens(string? label, string? rawLabel)`: Extracts meaningful alphanumeric tokens (filtering card masks `X\d{4}`, dates, and generic transaction words).
  - `IsLabelMatching(...)` & `IsTokenSetMatching(...)`: Inter-export fuzzy duplicate detection via token set overlap algorithms.
- **`RecurringCadenceAnalyzer` & `StatementRecurrenceFrequency`**:
  - `DetectCadence(IReadOnlyList<DateTime> orderedDates)`: Statistical cadence determination (monthly, quarterly, annual) based on median temporal intervals.
  - `AppliesToMonth(StatementRecurrenceFrequency frequency, int month)`: Evaluates recurrence applicability to a given calendar month.
  - `CalculateConfidenceScore(int occurrencesCount, decimal minAmount, decimal maxAmount)`: Confidence scoring heuristic (50% to 98%).
- **`BudgetPeriodCalculator`**:
  - `ResolvePeriod(...)`, `GetCurrentMonthRange(...)`, `GetLastMonthRange(...)`: Sliding period resolution (`two-weeks`, `three-months`, `six-months`, `all`, `custom`) and shifted budget/payroll cycles (configurable start day from 1 to 28).

### 3. `StatementForge.Models` — Data Contracts
- **`StatementTransaction`** (`sealed record`), **`StatementTransactionType`** (`enum`), **`CsvStatementOptions`** (`sealed record`).

---

## 📋 Maintenance Protocol for AI Agents

All enhancements to StatementForge must comply with:
1. **Backward Compatibility:** Preserve all existing public contracts. Use overloads or optional parameters.
2. **Code Cleanliness:** Zero warnings (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zero pragmas/suppressions, systematic `CancellationToken` on all async methods.
3. **Testing Standards:** Test naming convention `TargetMethod_ScenarioOrCondition_ExpectedResult`.
4. **Documentation:** Update both `FRAMEWORK.md` and `FRAMEWORK.fr.md` immediately upon introducing or modifying capabilities.
5. **Validation:** Full pass of `./verify.ps1`.
