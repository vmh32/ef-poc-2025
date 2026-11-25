using System.Net;
using System.Security.Authentication;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EdiConsole.Sourcing;
using EdiConsole.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace EdiConsole.Tests.Sourcing;

/// <summary>
/// <see cref="S3SourceCheck"/> against a mocked <see cref="IAmazonS3"/>: what it prints, and which event each kind of
/// failure logs. The exceptions are the types the SDK throws in each case. No network.
/// </summary>
public sealed class S3SourceCheckTests
{
    private const string Bucket = "edi-bucket";

    [Fact]
    public async Task An_existing_bucket_passes_and_the_check_names_the_bucket_and_the_endpoint()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeadBucketResponse());
        var logger = new RecordingLogger<S3SourceCheck>();
        var output = new StringWriter();

        var passed = await CreateCheck(s3, logger, serviceUrl: "http://127.0.0.1:5001").PassesAsync(output);

        Assert.True(passed);
        Assert.Equal("Using bucket edi-bucket on http://127.0.0.1:5001", output.ToString().TrimEnd());
        Assert.Empty(logger.Entries);
        s3.Verify(client => client.HeadBucketAsync(
            It.Is<HeadBucketRequest>(request => request.BucketName == Bucket), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Without_a_ServiceUrl_the_endpoint_is_AWS_in_the_region()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeadBucketResponse());
        var output = new StringWriter();

        await CreateCheck(s3, new RecordingLogger<S3SourceCheck>(), serviceUrl: "").PassesAsync(output);

        Assert.Equal("Using bucket edi-bucket on AWS us-east-1", output.ToString().TrimEnd());
    }

    [Theory]
    [InlineData("missing bucket", "BucketNotFound", "doesn't exist")]
    [InlineData("another region", "BucketInAnotherRegion", "isn't in us-east-1")]
    [InlineData("access denied", "S3CheckFailed", "HTTP 403 (Forbidden)")]
    [InlineData("nothing listening", "S3Unreachable", "Nothing answered")]
    [InlineData("no credentials", "S3RequestNotSent", "AWS_ACCESS_KEY_ID")]
    public async Task Each_failure_fails_the_check_and_logs_its_own_event(string failure, string eventName, string hint)
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ExceptionFor(failure));
        var logger = new RecordingLogger<S3SourceCheck>();

        var passed = await CreateCheck(s3, logger, serviceUrl: "http://127.0.0.1:5001").PassesAsync(TextWriter.Null);

        Assert.False(passed);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(eventName, entry.EventId.Name);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(hint, entry.Message);
        Assert.Null(entry.Exception);           // known setup problems: the message says it all, no stack trace
    }

    [Fact]
    public async Task The_reason_is_the_root_cause_not_the_outer_message()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(
                "The SSL connection could not be established, see inner exception.",
                new AuthenticationException("The remote certificate is invalid.")));
        var logger = new RecordingLogger<S3SourceCheck>();

        await CreateCheck(s3, logger, serviceUrl: "https://127.0.0.1:5001").PassesAsync(TextWriter.Null);

        var message = Assert.Single(logger.Entries).Message;
        Assert.EndsWith("Reason: The remote certificate is invalid.", message);
        Assert.DoesNotContain("see inner exception", message);
    }

    /// <summary>What the SDK throws in each case: an S3 answer (status and code), no answer, or no request sent.</summary>
    private static Exception ExceptionFor(string failure) => failure switch
    {
        "missing bucket" => new AmazonS3Exception("Not Found", ErrorType.Sender, "NotFound", "request-1", HttpStatusCode.NotFound),
        "another region" => new AmazonS3Exception("Moved Permanently", ErrorType.Sender, "MovedPermanently", "request-1", HttpStatusCode.MovedPermanently),
        "access denied" => new AmazonS3Exception("Forbidden", ErrorType.Sender, "Forbidden", "request-1", HttpStatusCode.Forbidden),
        "nothing listening" => new HttpRequestException("No connection could be made because the target machine actively refused it."),
        "no credentials" => new AmazonClientException("Failed to resolve AWS credentials."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "No exception for this case."),
    };

    private static S3SourceCheck CreateCheck(Mock<IAmazonS3> s3, ILogger<S3SourceCheck> logger, string serviceUrl) =>
        new(s3.Object, Options.Create(new S3SourceOptions { BucketName = Bucket, Region = "us-east-1", ServiceUrl = serviceUrl }), logger);
}
