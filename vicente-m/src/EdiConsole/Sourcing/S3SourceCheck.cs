using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdiConsole.Sourcing;

/// <summary>
/// <see cref="IFileSourceCheck"/> for S3: asks for the bucket (HeadBucket) and tells apart the usual reasons it fails.
/// </summary>
/// <remarks>
/// <para>
/// A HEAD response has no body, so S3 reports only a status: 404 for a missing bucket, 301 when the bucket is in
/// another region than the client's, 403 when access is denied. The 301 response names the bucket's region in a
/// header, but the SDK's exception doesn't expose headers, so the event can only say which region was wrong.
/// </para>
/// <para>
/// When no response comes back, the SDK throws other types: <see cref="HttpRequestException"/> when nothing answers
/// (after its own retries, which take several seconds), and <see cref="AmazonClientException"/> when it can't send the
/// request at all, most often because it found no credentials.
/// </para>
/// <para>
/// These are known setup problems, so the log carries the root cause's message, not the exception: its stack trace
/// shows only SDK and socket internals.
/// </para>
/// </remarks>
public sealed class S3SourceCheck(IAmazonS3 s3, IOptions<S3SourceOptions> options, ILogger<S3SourceCheck> logger)
    : IFileSourceCheck
{
    /// <summary>Prints the bucket and endpoint it's using to <paramref name="output"/>, then checks the bucket.</summary>
    public async Task<bool> PassesAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var bucket = settings.BucketName;
        var endpoint = string.IsNullOrWhiteSpace(settings.ServiceUrl) ? $"AWS {settings.Region}" : settings.ServiceUrl;
        output.WriteLine($"Using bucket {bucket} on {endpoint}");

        try
        {
            await s3.HeadBucketAsync(new HeadBucketRequest { BucketName = bucket }, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.BucketNotFound(bucket, endpoint);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.MovedPermanently)
        {
            logger.BucketInAnotherRegion(bucket, settings.Region);
        }
        catch (AmazonS3Exception ex)
        {
            logger.S3CheckFailed(bucket, endpoint, (int)ex.StatusCode, ex.ErrorCode ?? "none");
        }
        catch (HttpRequestException ex)
        {
            logger.S3Unreachable(endpoint, Reason(ex));
        }
        catch (AmazonClientException ex)
        {
            logger.S3RequestNotSent(bucket, Reason(ex));
        }

        return false;
    }

    /// <summary>
    /// The innermost exception's message: the actual cause, e.g. "No such host is known", where the outer message may
    /// only say "see inner exception".
    /// </summary>
    private static string Reason(Exception exception) => exception.GetBaseException().Message;
}
