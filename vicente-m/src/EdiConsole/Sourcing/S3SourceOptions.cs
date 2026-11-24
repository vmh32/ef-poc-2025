namespace EdiConsole.Sourcing;

/// <summary>
/// Settings for the S3 source, bound from the "S3Source" configuration section. Read only when
/// <c>FileSource:Kind</c> is S3. Credentials are not settings: the SDK's default chain provides them
/// (environment variables, a profile, or an IAM role).
/// </summary>
public sealed class S3SourceOptions
{
    public const string SectionName = "S3Source";

    /// <summary>The bucket to read from, e.g. edi-bucket.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>The folder inside the bucket, e.g. incoming/. A value without a trailing slash gets one.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Where requests go when the bucket is not on AWS, e.g. http://127.0.0.1:5001 for moto. LEAVE EMPTY FOR AWS.
    /// Write 127.0.0.1, not localhost: on Windows, localhost tries IPv6 first, which adds seconds per request.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>The AWS region, e.g. us-east-1. It signs every request and, without a ServiceUrl, picks the endpoint.</summary>
    public string Region { get; set; } = string.Empty;
}
