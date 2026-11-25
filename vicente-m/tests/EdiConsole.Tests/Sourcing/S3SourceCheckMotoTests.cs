using EdiConsole.Sourcing;
using EdiConsole.Tests.Support;

namespace EdiConsole.Tests.Sourcing;

/// <summary>
/// <see cref="S3SourceCheck"/> against the local moto server: what a real HeadBucket answers for a bucket that exists
/// and one that doesn't. Needs moto on port 5001.
/// </summary>
[Trait("Category", "Moto")]
public sealed class S3SourceCheckMotoTests
{
    [Fact]
    public async Task An_existing_bucket_passes()
    {
        await using var bucket = await MotoBucket.CreateAsync();
        var logger = new RecordingLogger<S3SourceCheck>();

        var passed = await bucket.CreateCheck(logger).PassesAsync(TextWriter.Null);

        Assert.True(passed);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_missing_bucket_logs_BucketNotFound()
    {
        await using var bucket = await MotoBucket.CreateAsync();
        var logger = new RecordingLogger<S3SourceCheck>();

        var passed = await bucket.CreateCheck(logger, bucketName: "no-such-" + bucket.Name).PassesAsync(TextWriter.Null);

        Assert.False(passed);
        Assert.Equal("BucketNotFound", Assert.Single(logger.Entries).EventId.Name);
    }
}
