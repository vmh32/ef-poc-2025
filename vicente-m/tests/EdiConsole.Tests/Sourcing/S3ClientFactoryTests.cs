using Amazon;
using EdiConsole.Sourcing;

namespace EdiConsole.Tests.Sourcing;

/// <summary>The SDK settings the app builds for S3. The settings for AWS, or for a local stand-in such as moto.</summary>
public sealed class S3ClientFactoryTests
{
    [Fact]
    public void With_a_ServiceUrl_the_client_uses_it_with_path_style_and_no_RegionEndpoint()
    {
        var config = S3ClientFactory.ConfigFor(new S3SourceOptions
        {
            BucketName = "edi-bucket",
            Region = "us-east-1",
            ServiceUrl = "http://127.0.0.1:5001",  //since it is not empty, the client uses this instead of AWS and sets ForcePathStyle and AuthenticationRegion
        });

        Assert.Equal("http://127.0.0.1:5001", new Uri(config.ServiceURL).GetLeftPart(UriPartial.Authority));
        Assert.Null(config.RegionEndpoint);   // must stay unset: with RegionEndpoint also set, requests go to AWS instead of ServiceURL
        Assert.True(config.ForcePathStyle);   // 
        Assert.Equal("us-east-1", config.AuthenticationRegion);
    }

    [Fact]
    public void Without_a_ServiceUrl_the_client_uses_the_region_endpoint()
    {
        var config = S3ClientFactory.ConfigFor(new S3SourceOptions { BucketName = "edi-bucket", Region = "eu-west-1" });

        Assert.Equal(RegionEndpoint.EUWest1, config.RegionEndpoint);
        Assert.False(config.ForcePathStyle);
    }
}
