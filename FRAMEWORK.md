# StatementForge — Spécification & Documentation IA du Framework

Ce document est le manifeste technique destiné à l'agent IA et aux développeurs. Il récapitule l'ensemble des capacités, composants et contrats publics exposés par **StatementForge**.

---

## 🎯 Périmètre & Responsabilité

StatementForge est la bibliothèque partagée .NET de traitement et de parsing haute performance de **relevés bancaires et formats d'échange financier** (OFX 1.x SGML, OFX 2.x XML, QFX, CSV/TSV configurables). Elle est 100% agnostique du domaine applicatif et des bases de données.

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
  - Parsing robuste des dates compactes bancaires (`YYYYMMDD[HHMMSS]`) avec nettoyage des fuseaux horaires `[0:GMT]`.
- **`CsvStatementParser`** :
  - Découpage CSV/TSV conforme RFC 4180 avec gestion des guillemets d'échappement et délimiteurs personnalisables.
  - Support des montants signés sur une colonne unique ou des colonnes Débit / Crédit distinctes.
  - Tolérance multi-culturelle pour les séparateurs décimaux (`,`, `.`), séparateurs de milliers, espaces insécables et symboles monétaires (`€`, `EUR`, `$`).
  - Nettoyage et normalisation des libellés bancaires.

### 2. `StatementForge.Models` — Contrats de données
- **`StatementTransaction`** (`sealed record`) :
  - `DateTime Date` : Date de valeur ou d'enregistrement de l'opération.
  - `decimal Amount` : Montant absolu de l'opération (toujours positif).
  - `StatementTransactionType Type` : Sens du flux (`Debit` ou `Credit`).
  - `string Label` : Libellé nettoyé de l'opération.
  - `string? RawLabel` : Libellé brut d'origine.
  - `string? Reference` : Référence ou identifiant unique d'opération (FITID, numéro de chèque).
  - `string? ExternalId` : Identifiant externe pérenne.
  - `string? CategoryCandidate` : Catégorie candidate extraite des méta-informations.
- **`StatementTransactionType`** (`enum`) :
  - `Debit` : Dépense / sortie de fonds.
  - `Credit` : Revenu / entrée de fonds.
- **`CsvStatementOptions`** (`sealed record`) :
  - Paramétrage complet du mapping de colonnes (`DateColumnIndex`, `LabelColumnIndex`, `AmountColumnIndex`, `DebitColumnIndex`, `CreditColumnIndex`, `ReferenceColumnIndex`), délimiteur (`Delimiter`), séparateur décimal (`DecimalSeparator`), format de date (`DateFormat`) et présence d'en-tête (`HasHeader`).

---

## 📋 Protocole de Maintenance par l'Agent IA

Toute modification de StatementForge doit respecter :
1. **Rétrocompatibilité :** Conserver tous les contrats publics existants. Utiliser des surcharges ou des paramètres optionnels.
2. **Qualité :** Zéro warning (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zéro pragma/suppress, `CancellationToken` systématique sur les I/O asynchrones.
3. **Tests :** Nommage `MethodeCible_ScenarioOuCondition_ResultatAttendu`.
4. **Mise à jour :** Compléter immédiatement ce fichier `FRAMEWORK.md` en cas d'ajout d'une nouvelle capacité.
5. **Validation :** Valider par `./verify.ps1`.
