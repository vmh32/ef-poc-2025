using System.Runtime.CompilerServices;
using EdiFabric.Core.Model.Edi;
using EdiFabric.Core.Model.Edi.ErrorContexts;
using EdiFabric.Core.Model.Edi.X12;
using EdiFabric.Framework.Readers;
using EdiFabric.Templates.Hipaa5010;

namespace EdiConsole.Parsing;

/// <summary>
/// <see cref="IEdiParser"/> on EdiFabric's <see cref="X12Reader"/>: reads a stream of X12 and yields one
/// <see cref="ParsedTransaction"/> per transaction set (ST…SE). Only 837P is supported.
/// </summary>
/// <remarks>
/// <para>
/// Reading only, no validation: an 837P with parse errors (e.g. an unexpected segment) is still yielded as a
/// transaction, because EdiFabric keeps those errors inside the message, where the validator turns them into
/// structured issues (segment, position, code). Reducing them to a string here would lose that detail.
/// </para>
/// <para>
/// The license must be set before the first read (Program.cs does it at startup).
/// </para>
/// <para>
/// Not done here, or anywhere yet: envelope validation (SNIP 1–2 for ISA, GS, GE, IEA).
/// The ISA is only read for ISA13 and ISA15. Its fields, the GS fields (e.g. GS01 = HC, GS08 = 005010X222A1)
/// and the trailers' control numbers and counts are never checked, and a missing IEA goes unnoticed.
/// A file with a wrong envelope but a valid 837P is accepted (e.g. samples/837-sample-file-2.edi).
/// </para>
/// </remarks>
public sealed class EdiFabricParser : IEdiParser
{
    // EdiFabric finds the transaction classes (TS837P...) in this assembly, by each file's GS08 version.
    private const string TemplatesAssembly = "EdiFabric.Templates.Hipaa";

    public async IAsyncEnumerable<ParsedTransaction> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // LeaveOpen: the stream belongs to the caller (IEdiFileSource.OpenReadAsync says the caller disposes it),
        // so disposing the reader must not close it.
        using var reader = new X12Reader(stream, TemplatesAssembly, new X12ReaderSettings { LeaveOpen = true });

        // The reader yields one item at a time, in file order: ISA, GS, the transaction(s), GE, IEA.
        // A transaction doesn't contain its envelope, so the ISA is kept here until the transactions that
        // follow it arrive, and replaced by the next ISA if the file holds several interchanges (ISA…IEA ISA…IEA).
        // Nullable: a transaction without a preceding ISA gets null envelope values instead of a crash.
        ISA? interchange = null;

        while (await reader.ReadAsync(cancellationToken))       // an empty file yields nothing at all
        {
            // Order matters: a switch takes the first matching case, and a TS837P is also an EdiMessage,
            // so the specific case must come before the general one (the compiler rejects it otherwise).
            switch (reader.Item)
            {
                case ISA isa:
                    interchange = isa;                          // remember it for the transactions that follow
                    break;

                case TS837P transaction:                        // what we support, with or without parse errors
                    yield return new ParsedTransaction(
                        interchange?.InterchangeControlNumber_13,
                        interchange?.UsageIndicator_15,
                        transaction.ST.TransactionSetControlNumber_02,
                        transaction,
                        Problem: null);
                    break;

                case EdiMessage other:                          // any other transaction set, e.g. a TS837I
                    yield return new ParsedTransaction(
                        interchange?.InterchangeControlNumber_13,
                        interchange?.UsageIndicator_15,
                        other.ErrorContext?.ControlNumber,
                        Transaction: null,
                        Problem: new ParseProblem(
                            ParseProblem.UnsupportedTransaction,
                            $"Unsupported transaction {other.GetType().Name} ({other.Version}): only 837P (005010X222A1) is ingested."));
                    break;

                case ReaderErrorContext error:                  // something the reader couldn't read, e.g. a 275 (no template)
                    yield return new ParsedTransaction(
                        interchange?.InterchangeControlNumber_13,
                        interchange?.UsageIndicator_15,
                        error.MessageErrorContext?.ControlNumber,
                        Transaction: null,
                        Problem: new ParseProblem(
                            ParseProblem.Unreadable,
                            $"{error.ReaderErrorCode}: {error.Message}"));     // EdiFabric's own code, e.g. InvalidSpecOrAssembly
                    break;

                case GS or GE or IEA:                           // envelope parts nothing in our model uses. NOT VALIDATED by anyone:
                    break;                                      // the reader doesn't compare GE/IEA with GS/ISA (control numbers,
                                                                // counts), and their own fields aren't checked either (see remarks).

                default:                                        // anything else: reject visibly, never ignore silently
                    yield return new ParsedTransaction(
                        interchange?.InterchangeControlNumber_13,
                        interchange?.UsageIndicator_15,
                        TransactionSetControlNumber: null,
                        Transaction: null,
                        Problem: new ParseProblem(
                            ParseProblem.UnexpectedItem,
                            $"Unexpected item from the reader: {reader.Item?.GetType().Name ?? "null"}."));
                    break;
            }
        }
    }
}
