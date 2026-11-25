using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EdiConsole.Sourcing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdiConsole.Tests.Support;

/// <summary>
/// A throwaway bucket on the local moto server. The name is random; the objects and the bucket are deleted on dispose.
/// The dummy credentials live here, in test code, never in configuration.
/// </summary>
internal sealed class MotoBucket : IAsyncDisposable
{
    /// <summary>
    /// moto's address. Use 127.0.0.1, not localhost: on Windows, localhost tries IPv6 first, which adds seconds per request.
    /// </summary>
    public const string Endpoint = "http://127.0.0.1:5001";

    private readonly IAmazonS3 _client;

    private MotoBucket(string name, IAmazonS3 client)
    {
        Name = name;
        _client = client;
    }

    public string Name { get; }

    /// <summary>Creates the bucket. Fails first, with a clear message, if moto isn't running.</summary>
    public static async Task<MotoBucket> CreateAsync()
    {
        await RequireRunningAsync();

        var name = "edi-test-" + Guid.NewGuid().ToString("N")[..12];
        var client = new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),   // dummy credentials: moto accepts any key while it isn't checking keys
            S3ClientFactory.ConfigFor(SettingsFor(name, string.Empty)));
        try
        {
            await client.PutBucketAsync(name);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new MotoBucket(name, client);
    }

    /// <summary>A source on this bucket, built with the app's settings and config, and the test's credentials.</summary>
    public S3FileSource CreateSource(string prefix, string searchPattern) =>
        new(_client, Options.Create(SettingsFor(Name, prefix)), Options.Create(new FileSourceOptions { SearchPattern = searchPattern }));

    /// <summary>A startup check on this bucket, or on <paramref name="bucketName"/> through the same client.</summary>
    public S3SourceCheck CreateCheck(ILogger<S3SourceCheck> logger, string? bucketName = null) =>
        new(_client, Options.Create(SettingsFor(bucketName ?? Name, string.Empty)), logger);

    public async Task PutAsync(string key, byte[] content)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Name,
            Key = key,
            InputStream = new MemoryStream(content),
        });
    }


    /// <summary>
    /// Deletes all objects in the bucket, then deletes the bucket. moto doesn't delete a bucket with objects in it.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            while (true)
            {
                var page = await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Name });
                if (page.S3Objects is null || page.S3Objects.Count == 0)
                {
                    break;
                }

                await _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = Name,
                    Objects = page.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList(),
                });
            }

            await _client.DeleteBucketAsync(Name);
        }
        finally
        {
            _client.Dispose();
        }
    }

    /// <summary>Fails with a clear message when moto isn't listening. Skipping would hide a server that was never started.</summary>
    public static async Task RequireRunningAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            await http.GetAsync(Endpoint + "/");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"moto isn't running on {Endpoint}. Start it with: moto_server -p 5001 " +
                "(install it with: pip install \"moto[server]>=5.0.0,<6.0.0\"), then run the Moto tests again.", ex);
        }
    }

    /// <summary>
    /// The settings the app would build for this bucket, with the test's dummy credentials. The prefix is optional.
    /// </summary>
    /// <param name="bucket"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    private static S3SourceOptions SettingsFor(string bucket, string prefix) => new()
    {
        BucketName = bucket,
        Prefix = prefix,
        ServiceUrl = Endpoint,
        Region = "us-east-1",
    };
}
