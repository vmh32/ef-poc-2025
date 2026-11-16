using EdiConsole.Domain.Tracking;
using EdiFabric.Core.ErrorCodes;
using EdiFabric.Core.Model.Edi;
using EdiFabric.Core.Model.Edi.ErrorContexts;
using EdiFabric.Templates.Hipaa5010;

namespace EdiConsole.Validation;

/// <summary>
/// <see cref="ITransactionValidator"/> on EdiFabric's <c>IsValid</c>, which covers SNIP levels 1–4.
/// Levels 5–7 (code sets, line of service, trading partner) are our own rules, added in Phase 3.
/// </summary>
/// <remarks>
/// <para>
/// EdiFabric reports a tree: the transaction (message-level codes, e.g. SegmentsCountNotMatching), its segments
/// (e.g. PRV at position 22: UnexpectedSegment) and their data elements (e.g. SBR08: InvalidCodeValue).
/// It is flattened into one <see cref="ValidationIssue"/> row per error, keeping the segment, position and loop.
/// </para>
/// <para>
/// Parse errors found while reading (an 837P with <c>HasErrors</c>) are reported here too: <c>IsValid</c> includes them.
/// Each validation spends EdiFabric operations.
/// </para>
/// </remarks>
public sealed class EdiFabricValidator : ITransactionValidator
{
    public IReadOnlyList<ValidationIssue> Validate(TS837P transaction, byte snipLevel)
    {
        var settings = new ValidationSettings { ValidationLevel = ToValidationLevel(snipLevel) };

        if (transaction.IsValid(out var result, settings))
            return [];

        var controlNumber = transaction.ST?.TransactionSetControlNumber_02 ?? result.ControlNumber;   // ST02
        return ToIssues(result, controlNumber);
    }

    private static ValidationLevel ToValidationLevel(byte snipLevel) => snipLevel switch
    {
        1 => ValidationLevel.SyntaxOnly_SNIP1,          // integrity: valid X12 (structure, data types, lengths)
        2 => ValidationLevel.LimitsAndCodes_SNIP2,      // requirements: the 837P guide's usage, code values, repeats
        3 => ValidationLevel.Balancing_SNIP3,           // balancing: totals add up (e.g. CLM02 = Σ SV102)
        4 => ValidationLevel.InterSegment_SNIP4,        // situational: "required when..." rules across segments
        _ => throw new ArgumentOutOfRangeException(nameof(snipLevel), snipLevel,
            "EdiFabric validates SNIP levels 1–4; levels 5–7 are our own rules (Phase 3)."),
    };

    private static List<ValidationIssue> ToIssues(MessageErrorContext result, string? controlNumber)
    {
        var issues = new List<ValidationIssue>();

        // Transaction level. 
        // MessageWithErrors is only EdiFabric's "this message has errors" summary:
        // storing it would duplicate the real rows below.
        foreach (var code in result.Codes.Where(c => c != MessageErrorCode.MessageWithErrors))
        {
            issues.Add(new ValidationIssue
            {
                TransactionSetControlNumber = controlNumber,
                Code = code.ToString(),
                Message = $"Transaction set {controlNumber}: {code}.",
            });
        }

        // Segment level.
        foreach (var segment in result.Errors)
        {
            // An unexpected segment belongs to no loop: EdiFabric gives "", but we store it as null to standardize.
            var loopId = string.IsNullOrEmpty(segment.LoopId) ? null : segment.LoopId;

            // Segment level, e.g. PRV at position 22: UnexpectedSegment.
            foreach (var code in segment.Codes)
            {
                issues.Add(new ValidationIssue
                {
                    TransactionSetControlNumber = controlNumber,
                    Segment = segment.Name,
                    SegmentPosition = segment.Position,
                    LoopId = loopId,
                    Code = code.ToString(),
                    // EdiFabric's message can be null OR empty: both get our fallback (?? would only catch null).
                    Message = string.IsNullOrWhiteSpace(segment.Message) ? $"{segment.Name}: {code}." : segment.Message,
                });
            }

            // Element level, e.g. SBR08: InvalidCodeValue. ValidationIssue has no component column,
            // so the full X12 location (with the component, e.g. CLM05-3) goes in the message.
            foreach (var element in segment.Errors)
            {
                var location = element.ComponentPosition > 0
                    ? $"{segment.Name}{element.Position:00}-{element.ComponentPosition}"
                    : $"{segment.Name}{element.Position:00}";
                var detail = string.IsNullOrWhiteSpace(element.Message) ? element.Code.ToString() : element.Message.Trim();

                issues.Add(new ValidationIssue
                {
                    TransactionSetControlNumber = controlNumber,
                    Segment = segment.Name,
                    SegmentPosition = segment.Position,
                    LoopId = loopId,
                    ElementPosition = element.Position,
                    Code = element.Code.ToString(),
                    Message = $"{location} (data element {element.Name}): {detail}",
                });
            }
        }

        return issues;
    }
}
