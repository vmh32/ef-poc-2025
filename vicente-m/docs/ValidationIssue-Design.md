# `ValidationIssue`: from EdiFabric's error tree to one row per problem

> How the columns of [`ValidationIssue`](../../src/EdiConsole/Domain/Tracking/ValidationIssue.cs) were derived from the errors EdiFabric reports, and how
> every other problem in the ingestion pipeline is stored in the same kind of row.
>
> Property names come from EdiFabric 11.1.0, read by reflection. Every example row is real: it was stored by ingesting
> [`samples/`](../../samples) at SNIP level 4 (2026-11-17).
>
> **Related:** [EdiFabric-Validation-Errors.md](EdiFabric-Validation-Errors.md) (the full tour of EdiFabric's error
> tree), [Validation-Layer-Design.md](Validation-Layer-Design.md) (the envelope checks we don't do yet).

1. [The tree EdiFabric reports](#1-the-tree-edifabric-reports)
2. [The three levels side by side](#2-the-three-levels-side-by-side)
3. [The flattened table](#3-the-flattened-table)
4. [Telling the level from the nulls](#4-telling-the-level-from-the-nulls)
5. [Other problems in the pipeline](#5-other-problems-in-the-pipeline)
6. [Duplicates: no rows at all](#6-duplicates-no-rows-at-all)

## 1. The tree EdiFabric reports

When a transaction fails `IsValid(out MessageErrorContext result, settings)`, `result` describes what's wrong as a tree,
with one level per X12 building block: the transaction, its segments, and their data elements.

```
MessageErrorContext                 the transaction (ST…SE)
 ├─ Codes        [...]              ← what's wrong with the transaction as a whole
 └─ Errors
     ├─ SegmentErrorContext         one segment with a problem
     │   ├─ Codes   [...]           ← what's wrong with the segment as a whole
     │   └─ Errors
     │       ├─ DataElementErrorContext   one element with a problem
     │       │   └─ Code               ← what's wrong with that element (ONE code)
     │       └─ ...
     └─ SegmentErrorContext ...
```

- A problem can sit at any level: a list of codes on the transaction, a list on each segment, exactly one code on each
  element.
- A segment can be in the tree with no codes of its own, only to hold its elements (`SBR` in §4).

## 2. The three levels side by side

Each column is one class of the tree, with every public property it has (11, 9 and 8). The highlighted ones reach
`ValidationIssue`.

| | `MessageErrorContext`<br>the transaction | `SegmentErrorContext`<br>one segment | `DataElementErrorContext`<br>one element |
|---|---|---|---|
| **Where** | ✅ **`ControlNumber`** (ST02) → `TransactionSetControlNumber` ¹ | ✅ **`Name`** → `Segment`<br>✅ **`Position`** → `SegmentPosition`<br>✅ **`LoopId`** → `LoopId` | ✅ **`Position`** → `ElementPosition`<br>📝 **`ComponentPosition`** → into `Message`<br>📝 **`Name`** → into `Message` |
| **What** | ✅ **`Codes`** (a list) → `Code`, one row per code ² | ✅ **`Codes`** (a list) → `Code`, one row per code | ✅ **`Code`** (just one) → `Code` |
| **Text** | `Message`: not used; the row gets ours instead | ✅ **`Message`** → `Message` | 📝 **`Message`** → into `Message` |
| **Children** | ↳ **`Errors`**: its segments | ↳ **`Errors`**: its elements | none: the bottom of the tree |
| **Not used** | `Name` · `HasErrors` · `Edition` · `Release` · `Index` · `ValidatedSegmentsCount` · `SpecRef` | `Value` · `SpecType` · `SpecRef` | `Value` · `RepetitionPosition` · `IsSituational` |

✅ copied into its own column · 📝 folded into `Message`, e.g. `SBR08 (data element 584): …`, or `CLM05-3` for a
component · ↳ walked to reach the next level, not stored

¹ The validator reads ST02 from the transaction's own `ST` segment and keeps `ControlNumber` as the fallback. Both
hold the same ST02.<br>
² Except `MessageWithErrors`, EdiFabric's "this message has errors" summary, which would only repeat the other rows.

**What's kept is exactly one row's worth of columns:**

- **where**, 5 fields → `TransactionSetControlNumber`, `Segment`, `SegmentPosition`, `LoopId`, `ElementPosition`
- **what**, the code from whichever level → `Code`
- **text** for people → `Message`

**Why the rest is dropped:**

- `Value` (segment and element) is raw text from the file and can hold patient data (names, birth dates). When it
  matters, EdiFabric's message already quotes it: "Invalid EDI code MC".
- `ComponentPosition` and the element's `Name` (its data element number, e.g. `584`) are rarely needed, so they live in
  `Message` instead of columns of their own.
- The transaction's `Name` is always `837` here, and `HasErrors` repeats the `false` that `IsValid` returned.
- The rest are EdiFabric internals (`SpecType`, `SpecRef`, `Index`, `Edition`, `Release`, `ValidatedSegmentsCount`)
  or details rarely needed to fix a file (`RepetitionPosition`, `IsSituational`).

## 3. The flattened table

One row per problem. Each row also carries what its parent nodes know (↓), so it can be read on its own, without the
tree.

| Column | SQL type | Transaction-level row | Segment-level row | Element-level row |
|---|---|---|---|---|
| `Id` | `bigint`, identity | ● | ● | ● |
| `IngestedFileId` | `bigint`, FK → `IngestedFiles` | ● its file | ● its file | ● its file |
| `TransactionSetControlNumber` | `nvarchar(9)`, nullable | ● ST02 | ↓ ST02 | ↓ ST02 |
| `Segment` | `nvarchar(3)`, nullable | — | ● `Name` | ↓ its segment's `Name` |
| `SegmentPosition` | `int`, nullable | — | ● `Position` | ↓ its segment's `Position` |
| `LoopId` | `nvarchar(50)`, nullable | — | ● `LoopId` (`""` → null) | ↓ its segment's `LoopId` |
| `ElementPosition` | `int`, nullable | — | — | ● `Position` |
| `Code` | `nvarchar(100)`, required | ● a `MessageErrorCode` | ● a `SegmentErrorCode` | ● a `DataElementErrorCode` |
| `Message` | `nvarchar(2000)`, required | ● ours: `Transaction set 0001: …` | ● EdiFabric's (ours if blank) | ● the location + EdiFabric's: `SBR08 (data element 584): …` |
| **Rows per node** | | one per code in `Codes` | one per code in `Codes`: **none** if it has no codes | one |

● filled from this level · ↓ copied down from the parent · — null

The code that flattens the tree is `ToIssues` in [`EdiFabricValidator`](../../src/EdiConsole/Validation/EdiFabricValidator.cs), with
one `foreach` per level. A file's `ErrorCount` ([`IngestedFile`](../../src/EdiConsole/Domain/Tracking/IngestedFile.cs)) is its number of rows.

## 4. Telling the level from the nulls

`837-sample-file-unexpected-segment.edi` has three problems in one transaction (ST02 `0001`), one at each level:

```
MessageErrorContext   ControlNumber "0001"
 ├─ Codes [SegmentsCountNotMatching]                          → row 1
 └─ Errors
     ├─ SegmentErrorContext   Name "SBR", Position 12, LoopId "2000B"
     │   ├─ Codes []                                          → no row: SBR is only there for its element
     │   └─ Errors
     │       └─ DataElementErrorContext   Position 8
     │           └─ Code InvalidCodeValue                     → row 2
     └─ SegmentErrorContext   Name "PRV", Position 22, LoopId ""
         ├─ Codes [UnexpectedSegment]                         → row 3
         └─ Errors []
```

They're stored as three rows:

| Id | `TransactionSetControlNumber` | `Segment` | `SegmentPosition` | `LoopId` | `ElementPosition` | `Code` | `Message` |
|---|---|---|---|---|---|---|---|
| 1 | `0001` | *null* | *null* | *null* | *null* | `SegmentsCountNotMatching` | Transaction set 0001: SegmentsCountNotMatching. |
| 2 | `0001` | `SBR` | `12` | `2000B` | `8` | `InvalidCodeValue` | SBR08 (data element 584): Invalid EDI code MC . |
| 3 | `0001` | `PRV` | `22` | *null* | *null* | `UnexpectedSegment` | Segment PRV\*PE\*PXC is not allowed in this position. Ensure that the segment ID and the EDI codes in positions 1 and 2, if any, match the specification. |

To find a row's level, read three columns, the **key**. The deeper the problem sits in the tree, the more of them are
filled:

| Row | `TransactionSetControlNumber` | `Segment` | `ElementPosition` | Level |
|:-:|:-:|:-:|:-:|---|
| 1 | ■ | □ | □ | transaction |
| 3 | ■ | ■ | □ | segment |
| 2 | ■ | ■ | ■ | element |

■ filled · □ null

Two columns are not part of the key:

- `SegmentPosition` is always filled together with `Segment`, so it adds nothing.
- **`LoopId` is null whenever the segment belongs to no loop.** Row 3 is a segment-level row, yet its `LoopId` is
  null: EdiFabric couldn't place the unexpected `PRV` in any loop. A null `LoopId` doesn't mean "transaction level".

## 5. Other problems in the pipeline

EdiFabric's validator isn't the only place where a file can go wrong.
[`IngestionService`](../../src/EdiConsole/Ingestion/IngestionService.cs) stores every other problem as a `ValidationIssue` too, so all
of a rejected file's problems are in one table:

```
IngestionService.IngestAsync(file)                           Code stored
│
├─ Read the bytes and hash them (SHA-256)
├─ Dedupe: is that hash already stored?
│     └─ yes → Duplicate: stop here, nothing is saved (§6)
│
├─ Parse: EdiFabricParser yields the file's items in order
│     ├─ ISA ............................................... no issue: ISA13, ISA15 → IngestedFiles
│     ├─ GS, GE, IEA ....................................... no issue: skipped
│     ├─ an 837P ........................................... on to Validate
│     ├─ another transaction (837I, 837D) .................. UnsupportedTransaction
│     ├─ something EdiFabric can't read (the 275) .......... Unreadable
│     ├─ anything else ..................................... UnexpectedItem
│     └─ no items at all (an empty file) ................... EmptyFile
│
├─ Validate the 837P: EdiFabricValidator
│     └─ invalid ........................................... EdiFabric's codes (§1–§4)
│
├─ Map it to claims: ClaimMapper
│     └─ a value the model needs is missing ................ UnmappableTransaction
│
└─ Decide and save
      any issue? → Rejected: the file + its issues (claims mapped so far are dropped)
      none?      → Accepted: the file + its claims
```

How each of our own codes fills the row:

| Code | Found in | Written by | `TransactionSetControlNumber` | `Segment`…`ElementPosition` | Example from the samples |
|---|---|---|---|---|---|
| `UnsupportedTransaction` | Parse | [`EdiFabricParser`](../../src/EdiConsole/Parsing/EdiFabricParser.cs), as a [`ParseProblem`](../../src/EdiConsole/Parsing/ParseProblem.cs) the facade copies | the transaction's ST02 | null | `837i-sample.edi`, ST02 `987654`: "Unsupported transaction TS837I (005010X223A2): only 837P (005010X222A1) is ingested." |
| `Unreadable` | Parse | `EdiFabricParser`, as a `ParseProblem` | ST02 if EdiFabric found one, else null | null | `sample_275_with_attachment.edi`, ST02 `0001`: "InvalidSpecOrAssembly: Type with attribute'[Message(X12, 005010X215, 275)]' was not found in assembly …" |
| `UnexpectedItem` | Parse | `EdiFabricParser`, as a `ParseProblem` | null | null | none: a safety net for any item the parser has no case for |
| `EmptyFile` | after Parse | `IngestionService`, when the parser yielded nothing | null | null | `TestClaimPaymentEmpty.edi`: "The file contains no transaction sets." |
| `UnmappableTransaction` | Map | `IngestionService`, when [`ClaimMapper`](../../src/EdiConsole/Mapping/ClaimMapper.cs) throws `InvalidDataException` | the transaction's ST02 | null | none: a safety net for a valid 837P that lacks a value the model needs. The message names it, e.g. "2010AA NM109 (billing provider NPI) is missing." |

None of these problems points at a segment or an element, so they only use the top of the row: **with ST02 they look
like transaction-level rows; without it, they point at the file as a whole.** The shape can't tell `UnsupportedTransaction`
(ours) from `SegmentsCountNotMatching` (EdiFabric's), so **`Code` is what tells the source apart**: these five codes
are ours, and any other code is EdiFabric's. That's why `Code` is a string and not an enum.

The full key, with the file level added:

| `TransactionSetControlNumber` | `Segment` | `ElementPosition` | Points at | Codes |
|:-:|:-:|:-:|---|---|
| □ | □ | □ | **the file**: no transaction identified | `EmptyFile`, `UnexpectedItem`; `Unreadable` when no ST02 was found |
| ■ | □ | □ | **one transaction** | EdiFabric's transaction codes, `UnsupportedTransaction`, `Unreadable`, `UnmappableTransaction` |
| ■ | ■ | □ | **one segment** | EdiFabric's segment codes |
| ■ | ■ | ■ | **one element** | EdiFabric's element codes |

The same rule in SQL:

```sql
SELECT Id, Code,
       CASE WHEN TransactionSetControlNumber IS NULL THEN 'file'
            WHEN Segment IS NULL                     THEN 'transaction'
            WHEN ElementPosition IS NULL             THEN 'segment'
            ELSE 'element'
       END AS Level
FROM ValidationIssues
ORDER BY Id;
```

Its output on the sample run:

```
Id  Code                      Level
1   SegmentsCountNotMatching  transaction
2   InvalidCodeValue          element
3   UnexpectedSegment         segment
4   UnsupportedTransaction    transaction
5   UnsupportedTransaction    transaction
6   Unreadable                transaction
7   EmptyFile                 file
```

## 6. Duplicates: no rows at all

A duplicate is a file whose bytes (SHA-256) match a file ingested before, accepted or rejected. `IngestAsync` stops at
the dedupe step, before parsing. It never creates an `IngestedFile`, so there's no `IngestedFiles` row and no
`ValidationIssues` row:

```
samples/  (9 files, in processing order)      IngestedFiles  (8 rows)
837-sample-file-2.edi                     →   1  Accepted   1 claim
837-sample-file-unexpected-segment.edi    →   2  Rejected   3 issues   the three rows of §4
837-sample-file.edi                       →   3  Accepted   2 claims   SHA-256 93cb52a64ed1…
837-sample.edi                            →   4  Rejected   1 issue    UnsupportedTransaction (an 837D)
837i-sample.edi                           →   5  Rejected   1 issue    UnsupportedTransaction (an 837I)
837p-sample.edi                           →   no row: Duplicate        SHA-256 93cb52a64ed1…, the same bytes as row 3
ClaimPayment.edi                          →   6  Accepted   1 claim
sample_275_with_attachment.edi            →   7  Rejected   1 issue    Unreadable
TestClaimPaymentEmpty.edi                 →   8  Rejected   1 issue    EmptyFile
```

The only trace of a duplicate is its console line:

```
  837p-sample.edi                          Duplicate  already ingested: skipped
```

Why there's no row:

- **Nothing new to record.** Row 3 already says what happened to those bytes.
- **Nothing to attach an issue to.** Every `ValidationIssue` needs an `IngestedFileId`, and `IngestedFiles.Sha256` is
  unique, so a second row for the same bytes couldn't be saved.
- **It costs nothing.** The check runs before parsing, so no EdiFabric operation is spent.

Worth knowing:

- Only the first file name is stored: `837p-sample.edi` appears nowhere in the database.
- **Every empty file is a duplicate of the first one.** Row 8's hash starts with `e3b0c442…`, the SHA-256 of zero
  bytes, so any later empty file, whatever its name, is reported as `Duplicate`, not `EmptyFile`.
- The only other outcome without rows is a file that **FAILED**: an exception such as an I/O or database error. Nothing
  is stored for it (the save is a single `SaveChanges`), so the next run tries it again.
