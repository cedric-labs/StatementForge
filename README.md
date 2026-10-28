# StatementForge

<p align="center">
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support curvecoder on Ko-fi" /></a>
</p>

High-performance, domain-agnostic .NET streaming parser and algorithmic analyzer for financial statements (OFX 1.x/2.x, QFX, delimited CSV/TSV) featuring label normalization, fuzzy duplicate detection, recurring cadence analysis, and sliding budget period calculations.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Framework](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)](https://dotnet.microsoft.com/)
[![Documentation: English](https://img.shields.io/badge/Docs-English-blue.svg)](FRAMEWORK.md)
[![Documentation: Français](https://img.shields.io/badge/Docs-Français-green.svg)](FRAMEWORK.fr.md)

> 📖 **Framework Documentation**: [English Specification](FRAMEWORK.md) | [Spécification en français](FRAMEWORK.fr.md)

---

## Key Capabilities

### 1. Banking Format Streaming Parsers (`StatementForge.Parsing`)
- **`IBankStatementParser`**: Unified asynchronous streaming interface.
- **`OfxStatementParser`**:
  - Native parser for OFX 1.x (SGML unclosed tags) and OFX 2.x (XML standards) without external heavy dependencies.
  - Streaming block segmentation (`<STMTTRN>`) with strict ReDoS timeouts.
  - Non-destructive static banking account extraction via `ExtractOfxAccountIdAsync` (`<ACCTID>`).
  - Signed amount normalization (`TRNAMT`) into explicit Debit/Credit representations and ISO banking timestamp extraction (`YYYYMMDD`).
- **`CsvStatementParser`**:
  - RFC 4180 compliant CSV and TSV parsing with configurable column index mappings (`CsvStatementOptions`).
  - Multi-cultural parsing tolerance for comma/period decimal notations, currency symbols (`€`, `$`, `EUR`, `USD`), and whitespace.

### 2. Algorithmic Statement Intelligence (`StatementForge.Analysis`)
- **`StatementLabelAnalyzer`**:
  - Cleans banking noise prefixes (`PRLV SEPA`, `VIR`, `CB`, transaction dates, card masks `X4242`) while isolating business descriptors.
  - Title Case emitter normalization and wildcard/regex pattern matching.
  - Inter-export fuzzy duplicate detection based on token set overlap.
- **`RecurringCadenceAnalyzer`**:
  - Statistical cadence detection for monthly, quarterly, and annual subscription cycles.
  - Algorithmic confidence scoring (50% to 98%).
- **`BudgetPeriodCalculator`**:
  - Computes dynamic rolling periods (`two-weeks`, `three-months`, `six-months`) and customizable budget cycles (start days 1 to 28).

---

## Installation

Add the package via the .NET CLI:

```bash
dotnet add package StatementForge --version 1.1.0
```

---

## Quick Example

```csharp
using StatementForge.Parsing;

var parser = new OfxStatementParser();

using var fileStream = File.OpenRead("bank_statement.ofx");
var transactions = await parser.ParseAsync(fileStream);

foreach (var tx in transactions)
{
    Console.WriteLine($"{tx.Date:yyyy-MM-dd} | {tx.Type} | {tx.Amount} | {tx.Label}");
}
```

---

## License

This project is licensed under the [MIT License](LICENSE) &copy; 2026 curvecoder.
