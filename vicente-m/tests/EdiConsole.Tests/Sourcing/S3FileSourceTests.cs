using Amazon.S3;
using Amazon.S3.Model;
using EdiConsole.Sourcing;
using EdiConsole.Tests.Support;
using Microsoft.Extensions.Options;
using Moq;

namespace EdiConsole.Tests.Sourcing;

/// <summary>
/// <see cref="S3FileSource"/> against a mocked <see cref="IAmazonS3"/>: the listing rules, paging and reading.
/// No network. The moto tests cover the real service.
/// </summary>
public sealed class S3FileSourceTests
{
    private const string Bucket = "edi-bucket";

    [Fact]
    public async Task Lists_the_matching_names_in_name_order_and_skips_the_rest()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(false, null, "incoming/b.edi", "incoming/x.edix", "incoming/w.EDI", "incoming/y.edia", "incoming/a.edi", "incoming/z.edi.bak"));

        var names = (await CreateSource(s3).ListFilesAsync()).Select(file => file.Name);

        Assert.Equal(["a.edi", "b.edi", "w.EDI"], names);
    }

    [Fact]
    public async Task Follows_the_continuation_tokens_across_pages_with_and_without_common_prefixes()
    {
        var firstPage = Page(true, "token-2", "incoming/a.edi");
        firstPage.CommonPrefixes = ["incoming/sub/"];            // this page has a subfolder, so the SDK fills in CommonPrefixes
        var secondPage = Page(false, null, "incoming/b.edi");    // this one has none, so the SDK leaves CommonPrefixes null
        var pages = new Queue<ListObjectsV2Response>(new[] { firstPage, secondPage });
        var requests = new List<ListObjectsV2Request>();
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .Callback<ListObjectsV2Request, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => pages.Dequeue());

        var names = (await CreateSource(s3).ListFilesAsync()).Select(file => file.Name);

        Assert.Equal(["a.edi", "b.edi"], names);
        Assert.Equal(2, requests.Count);
        Assert.Null(requests[0].ContinuationToken);
        Assert.Equal("token-2", requests[1].ContinuationToken);
    }

    [Theory]
    [InlineData("incoming", "incoming/", "s3://edi-bucket/incoming/")]
    [InlineData("incoming/", "incoming/", "s3://edi-bucket/incoming/")]
    [InlineData("", "", "s3://edi-bucket/")]
    public async Task The_prefix_gets_a_trailing_slash_and_names_the_listing(string configured, string expectedPrefix, string expectedDescription)
    {
        ListObjectsV2Request? seen = null;
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .Callback<ListObjectsV2Request, CancellationToken>((request, _) => seen = request)
            .ReturnsAsync(Page(false, null, expectedPrefix + "a.edi"));
        var source = CreateSource(s3, prefix: configured);

        // the s3.ListObjectsV2Async callback records the request in seen, and returns a page with one file under the prefix. 
        // The test checks that the prefix is normalized to have a trailing slash, 
        // and that the source description is correct.

        var files = await source.ListFilesAsync();

        Assert.Equal(expectedDescription, source.Description);
        Assert.Equal(expectedPrefix, seen!.Prefix);
        Assert.Equal("/", seen.Delimiter);
        Assert.Equal(["a.edi"], files.Select(file => file.Name));
        Assert.Equal(expectedDescription + "a.edi", files[0].SourceUri);
    }

    [Fact]
    public async Task The_search_pattern_comes_from_FileSource_SearchPattern()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(false, null, "incoming/a.edi", "incoming/y.edia"));

        var names = (await CreateSource(s3, pattern: "*.edia").ListFilesAsync()).Select(file => file.Name);

        Assert.Equal(["y.edia"], names);
    }

    [Fact]
    public async Task An_empty_listing_gives_no_files()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(false, null));

        Assert.Empty(await CreateSource(s3).ListFilesAsync());
    }

    [Fact]
    public async Task OpenReadAsync_returns_the_object_bytes_for_the_key_in_the_source_uri()
    {
        var content = "any bytes will do, nothing is parsed here"u8.ToArray();
        GetObjectRequest? seen = null;
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))   // fake GetObjectAsync: record the request in seen, return a response whose stream holds content
            .Callback<GetObjectRequest, CancellationToken>((request, _) => seen = request)
            .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(content) });

        await using var stream = await CreateSource(s3).OpenReadAsync(new EdiFile("a.edi", "s3://edi-bucket/incoming/a.edi"));
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal(Bucket, seen!.BucketName);
        Assert.Equal("incoming/a.edi", seen.Key);
        Assert.Equal(content, copy.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_refuses_a_file_from_another_bucket()
    {
        var source = CreateSource(new Mock<IAmazonS3>());

        await Assert.ThrowsAsync<ArgumentException>(() => source.OpenReadAsync(new EdiFile("a.edi", "s3://other-bucket/incoming/a.edi")));
    }

    private static S3FileSource CreateSource(Mock<IAmazonS3> s3, string prefix = "incoming/", string pattern = "*.edi") =>
        new(
            s3.Object,
            Options.Create(new S3SourceOptions { BucketName = Bucket, Prefix = prefix, Region = "us-east-1" }),
            Options.Create(new FileSourceOptions { SearchPattern = pattern }));

    /// <summary>A listing page the way the SDK returns it: CommonPrefixes stays null unless a test sets it.</summary>
    private static ListObjectsV2Response Page(bool truncated, string? nextToken, params string[] keys) => new()
    {
        S3Objects = keys.Select(key => new S3Object { Key = key }).ToList(),
        IsTruncated = truncated,
        NextContinuationToken = nextToken,
    };
}
