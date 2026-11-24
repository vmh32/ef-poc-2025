using Amazon;
using Amazon.S3;

namespace EdiConsole.Sourcing;

/// <summary>Builds the S3 client from <see cref="S3SourceOptions"/>.</summary>
public static class S3ClientFactory
{
    /// <summary>
    /// The SDK settings for these options. With a ServiceUrl, the client uses that address, path-style addressing and
    /// the signing region. RegionEndpoint is never set together with a ServiceUrl: setting both makes the SDK ignore
    /// the ServiceUrl and send the requests to AWS.
    /// </summary>
    public static AmazonS3Config ConfigFor(S3SourceOptions options)
    {
        //fore real S3, the ServiceUrl is null and the RegionEndpoint is set.

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            return new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region) };
        }

        // For moto, the ServiceUrl is set and the RegionEndpoint is null.
        
        return new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,                // moto and other S3 stand-ins take http://host:port/bucket/key
            AuthenticationRegion = options.Region,
        };
    }

    /// <summary>
    /// The client the app uses. Its credentials come from the SDK's default chain, never from the settings.
    /// </summary>
    public static IAmazonS3 Create(S3SourceOptions options) => new AmazonS3Client(ConfigFor(options));
}
