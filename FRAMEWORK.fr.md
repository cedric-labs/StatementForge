<p align="right">
  <a href="FRAMEWORK.md">English</a> | <b>Français</b>
</p>

# StatementForge — Spécification & Documentation IA du Framework

Ce document est le manifeste technique destiné à l'agent IA et aux développeurs. Il récapitule l'ensemble des capacités, composants et contrats publics exposés par **StatementForge**.

---

## 🎯 Périmètre & Responsabilité

StatementForge est la bibliothèque partagée .NET de traitement, de parsing et d'analyse algorithmique de **relevés bancaires et formats d'échange financier** (OFX 1.x SGML, OFX 2.x XML, QFX, CSV/TSV configurables, normalisation de libellés, détection de doublons, détection de récurrence et calcul de périodes glissantes/salariales). Elle est 100% agnostique du domaine applicatif et des bases de données.

---

## 🧱 Composants & Capacités Détaillées

### 1. `StatementForge.Parsing` — Analyseurs de flux bancaires
- **`IBankStatementParser`** (interface) :
  - `string SupportedFormat` : Identifiant du format supporté (`OFX`, `CSV`).
  - `bool CanHandle(string fileName, Stream stream)` : Détection par extension et inspection non destructive des premiers octets du flux.
  - `Task<IReadOnlyList<StatementTransaction>> ParseAsync(Stream stream, CsvStatementOptions? options, CancellationToken cancellationToken)` : Extraction asynchrone des transactions normalisées avec annulation coopérative.
- **`OfxStatementParser`** :
  - Supporte les formats OFX 1.x (SGML avec balises non fermées) et OFX 2.x (XML standard).
  - Découpage par blocs `<STMTTRN>` avec protection anti-ReDoS (délais d'expiration stricts sur les expressions régulières).
  - `ExtractOfxAccountIdAsync(Stream stream, CancellationToken cancellationToken)` : Extraction statique non destructive de l'identifiant de compte bancaire (`<ACCTID>`).
  - Normalisation des montants signés (`TRNAMT`) et conversion en types Débit / Crédit stricts.
  - Parsing des dates compactes bancaires (`YYYYMMDD[HHMMSS]`) avec nettoyage des fuseaux horaires `[0:GMT]`.
- **`CsvStatementParser`** :
  - Découpage CSV/TSV conforme RFC 4180 avec gestion des guillemets d'échappement et délimiteurs personnalisables.
  - Support des montants signés sur une colonne unique ou des colonnes Débit / Crédit distinctes.
  - Tolérance multi-culturelle pour les séparateurs décimaux (`,`, `.`), séparateurs de milliers, espaces insécables et symboles monétaires (`€`, `EUR`, `$`).

### 2. `StatementForge.Analysis` — Normalisation, Doublons, Récurrence & Périodes
- **`StatementLabelAnalyzer`** :
  - `CleanTransactionLabel(string label, string? rawLabel)` : Nettoyage des préfixes bancaires (`PRLV SEPA`, `VIR`, `CB`, dates, identifiants numériques) et isolation des descripteurs de contrats (`AUTO`, `HABITATION`, `SANTE`, `BOX`, `MOBILE`, etc.).
  - `FormatSuggestedName(string pattern)` : Formatage Title Case d'un émetteur normalisé.
  - `MatchesPattern(string label, string? rawLabel, string pattern)` : Évaluation insensible à la casse par sous-chaîne, joker (`*`, `%`) ou expression régulière protégée contre le ReDoS.
  - `MatchesAmount(decimal expectedAmount, decimal tolerancePercentage, decimal transactionAmount)` : Vérification d'appartenance d'un montant à une plage de tolérance relative.
  - `ExtractMeaningfulTokens(string? label, string? rawLabel)` : Extraction des jetons alphanumériques significatifs (hors masques de carte `X\d{4}`, dates et mots-clés bancaires génériques).
  - `IsLabelMatching(...)` & `IsTokenSetMatching(...)` : Détection floue de doublons inter-exports par recouvrement d'ensembles de jetons.
- **`RecurringCadenceAnalyzer` & `StatementRecurrenceFrequency`** :
  - `DetectCadence(IReadOnlyList<DateTime> orderedDates)` : Détection statistique des fréquences mensuelles, trimestrielles et annuelles à partir des écarts temporels.
  - `AppliesToMonth(StatementRecurrenceFrequency frequency, int month)` : Détermination d'applicabilité d'une charge sur un mois donné.
  - `CalculateConfidenceScore(int occurrencesCount, decimal minAmount, decimal maxAmount)` : Calcul du score de confiance (50 à 98%).
- **`BudgetPeriodCalculator`** :
  - `ResolvePeriod(...)`, `GetCurrentMonthRange(...)`, `GetLastMonthRange(...)` : Calcul de périodes glissantes (`two-weeks`, `three-months`, `six-months`, `all`, `custom`) et de cycles budgétaires/salariaux décalés (jour de début de mois paramétrable de 1 à 28).

### 3. `StatementForge.Models` — Contrats de données
- **`StatementTransaction`** (`sealed record`), **`StatementTransactionType`** (`enum`), **`CsvStatementOptions`** (`sealed record`).

---

## 📋 Protocole de Maintenance par l'Agent IA

Toute modification de StatementForge doit respecter :
1. **Rétrocompatibilité :** Conserver tous les contrats publics existants. Utiliser des surcharges ou des paramètres optionnels.
2. **Qualité :** Zéro warning (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zéro pragma/suppress, `CancellationToken` systématique sur les I/O asynchrones.
3. **Tests :** Nommage `MethodeCible_ScenarioOuCondition_ResultatAttendu`.
4. **Mise à jour :** Compléter immédiatement ce fichier `FRAMEWORK.md` en cas d'ajout d'une nouvelle capacité.
5. **Validation :** Valider par `./verify.ps1`.
