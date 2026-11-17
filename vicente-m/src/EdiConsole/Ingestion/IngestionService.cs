using System.Security.Cryptography;
using EdiConsole.Domain.Claims;
using EdiConsole.Domain.Tracking;
using EdiConsole.Mapping;
using EdiConsole.Parsing;
using EdiConsole.Persistence;
using EdiConsole.Sourcing;
using EdiConsole.Validation;
using EdiFabric.Framework.Readers;
using Microsoft.Extensions.Options;

namespace EdiConsole.Ingestion;

/// <summary>
/// The facade: ingests one file from start to finish (read, hash, dedupe, parse, validate, map, save)
/// and applies the policy: a file is accepted only if everything in it is valid.
/// </summary>
/// <remarks>
/// <para>
/// All or nothing per file: any issue in any transaction rejects the whole file. A rejected file is saved with its
/// issues and no claims; an accepted file with its claims and no issues. Either way, one <c>SaveChanges</c>.
/// </para>
/// <para>
/// A duplicate (same bytes as any file ingested before, accepted or rejected) is skipped before parsing: no row is
/// written.
/// </para>
/// <para>
/// Not validated yet: the envelope (ISA/GS/GE/IEA, SNIP 1–2) and SNIP 5–7. Only each transaction (ST…SE) is
/// validated, through EdiFabric at SNIP 1–4.
/// </para>
/// </remarks>
public sealed class IngestionService(
    IEdiFileSource source,
    IEdiParser parser,
    ITransactionValidator validator,
    IClaimMapper mapper,
    IIngestionStore store,
    IOptions<IngestionOptions> options,
    TimeProvider timeProvider) : IIngestionService
{
    /// <summary>Issue code for a file with no transaction sets in it.</summary>
    public const string EmptyFileCode = "EmptyFile";

    /// <summary>Issue code for a valid 837P the mapper still couldn't map (a required value missing).</summary>
    public const string UnmappableTransactionCode = "UnmappableTransaction";

    // Stored on every file, e.g. "11.1.0", so a result can be traced back to the EdiFabric version that produced it.
    private static readonly string EdiFabricVersion =
        typeof(X12Reader).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    public async Task<IngestionResult> IngestAsync(EdiFile file, CancellationToken cancellationToken = default)
    {
        var receivedAt = timeProvider.GetUtcNow();

        // 1–2. Read and hash. The bytes are kept in memory because both the hash and the parser need them,
        // and a stream from S3 (Phase 2) can't be rewound. Fine for EDI files of a few KB to a few MB.
        var content = await ReadAllAsync(file, cancellationToken);
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        // 3. Dedupe, before EdiFabric is used.
        if (await store.IsAlreadyIngestedAsync(sha256, cancellationToken))
            return new IngestionResult(file.Name, IngestionOutcome.Duplicate, ClaimCount: 0, ErrorCount: 0);

        var snipLevel = options.Value.SnipLevel;
        var ingestedFile = new IngestedFile
        {
            FileName = file.Name,
            Sha256 = sha256,
            ReceivedAt = receivedAt,
            SnipLevelApplied = snipLevel,
            EdiFabricVersion = EdiFabricVersion,
        };

        var claims = new List<Claim>();
        var issues = new List<ValidationIssue>();
        var transactionCount = 0;

        // 4. Parse, then validate and map each transaction. Validation goes on after the first invalid
        // transaction, so a rejection lists every problem in the file, not just the first.
        using var stream = new MemoryStream(content, writable: false);
        await foreach (var parsed in parser.ParseAsync(stream, cancellationToken))
        {
            transactionCount++;
            ingestedFile.InterchangeControlNumber ??= parsed.InterchangeControlNumber;   // the file's first interchange
            ingestedFile.UsageIndicator ??= parsed.UsageIndicator;

            if (parsed.Problem is { } problem)                                           // not an 837P, or unreadable
            {
                issues.Add(new ValidationIssue
                {
                    TransactionSetControlNumber = parsed.TransactionSetControlNumber,
                    Code = problem.Code,
                    Message = problem.Message,
                });
                continue;
            }

            var transaction = parsed.Transaction!;
            var transactionIssues = validator.Validate(transaction, snipLevel);
            if (transactionIssues.Count > 0)                                             // invalid: never mapped
            {
                issues.AddRange(transactionIssues);
                continue;
            }

            try
            {
                claims.AddRange(mapper.Map(transaction));
            }
            catch (InvalidDataException ex)                                             // valid, yet a required value is missing
            {
                issues.Add(new ValidationIssue
                {
                    TransactionSetControlNumber = parsed.TransactionSetControlNumber,
                    Code = UnmappableTransactionCode,
                    Message = ex.Message,
                });
            }
        }

        if (transactionCount == 0)                                                       // the parser yields nothing for an empty file
        {
            issues.Add(new ValidationIssue { Code = EmptyFileCode, Message = "The file contains no transaction sets." });
        }

        // 5. Decide: all or nothing.
        if (issues.Count == 0)
        {
            ingestedFile.Status = IngestionStatus.Accepted;
            ingestedFile.Claims.AddRange(claims);
        }
        else
        {
            ingestedFile.Status = IngestionStatus.Rejected;
            ingestedFile.Issues.AddRange(issues);                                        // claims mapped so far are dropped
        }

        ingestedFile.ClaimCount = ingestedFile.Claims.Count;
        ingestedFile.ErrorCount = ingestedFile.Issues.Count;
        ingestedFile.CompletedAt = timeProvider.GetUtcNow();

        // 6. Save the file with its claims or issues, in one SaveChanges.
        await store.SaveAsync(ingestedFile, cancellationToken);

        // 7. Report.
        var outcome = ingestedFile.Status == IngestionStatus.Accepted ? IngestionOutcome.Accepted : IngestionOutcome.Rejected;
        return new IngestionResult(file.Name, outcome, ingestedFile.ClaimCount, ingestedFile.ErrorCount);
    }

    private async Task<byte[]> ReadAllAsync(EdiFile file, CancellationToken cancellationToken)
    {
        await using var stream = await source.OpenReadAsync(file, cancellationToken);   // the source's stream: ours to dispose
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
