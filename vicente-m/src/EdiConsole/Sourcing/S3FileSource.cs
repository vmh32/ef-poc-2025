using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace EdiConsole.Sourcing;

/// <summary>
/// <see cref="IEdiFileSource"/> on an S3 bucket. Lists the objects directly under the prefix whose names match
/// <see cref="FileSourceOptions.SearchPattern"/>. Subfolders are skipped, as the local source skips them.
/// </summary>
public sealed class S3FileSource(IAmazonS3 s3, IOptions<S3SourceOptions> s3Options, IOptions<FileSourceOptions> fileOptions)
    : IEdiFileSource
{
    private const string Scheme = "s3://";

    private readonly IAmazonS3 _s3 = s3;
    private readonly string _bucket = s3Options.Value.BucketName;
    private readonly string _prefix = NormalizePrefix(s3Options.Value.Prefix);
    private readonly string _searchPattern = fileOptions.Value.SearchPattern;

    /// <summary>The bucket and prefix, as the run report shows them, e.g. s3://edi-bucket/incoming/.</summary>
    public string Description => $"{Scheme}{_bucket}/{_prefix}";

    /// <summary>
    /// Lists the matching files sorted by name, so every run processes them in the same order. All listing pages are
    /// read first, because the names have to be sorted before the first one is returned.
    /// </summary>
    public async IAsyncEnumerable<EdiFile> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var names = new List<string>();
        string? continuationToken = null;

        do
        {
            var page = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = _prefix,
                Delimiter = "/",                  // keys with a further "/" roll up into CommonPrefixes, which we do not read
                ContinuationToken = continuationToken,
            }, cancellationToken);

            // The SDK can return null, not an empty list, for an empty collection (it does so for CommonPrefixes).
            foreach (var s3Object in page.S3Objects ?? new List<S3Object>())
            {
                var name = s3Object.Key[_prefix.Length..];
                if (name.Length > 0 && name.IndexOf('/') < 0
                    && FileSystemName.MatchesSimpleExpression(_searchPattern, name, ignoreCase: true))
                {
                    names.Add(name);
                }
            }

            continuationToken = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        foreach (var name in names.Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new EdiFile(name, $"{Scheme}{_bucket}/{_prefix}{name}");   // SourceUri = s3://bucket/key
        }
    }

    /// <summary>
    /// Downloads the object into memory, so the caller gets a seekable stream. The caller disposes it.
    /// </summary>
    /// <exception cref="ArgumentException">The file's address is not an s3:// address in this source's bucket.</exception>
    /// <exception cref="AmazonS3Exception">The object is gone (NoSuchKey) or the request failed.</exception>
    public async Task<Stream> OpenReadAsync(EdiFile file, CancellationToken cancellationToken = default)
    {
        var (bucket, key) = ParseSourceUri(file.SourceUri);
        if (bucket != _bucket)
        {
            throw new ArgumentException($"{file.SourceUri} is not in this source's bucket, {_bucket}.", nameof(file));
        }

        using var response = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken);
        var copy = new MemoryStream();
        await response.ResponseStream.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        return copy;
    }

    /// <summary>Splits s3://bucket/key into its bucket and key. A bucket name never contains a slash.</summary>
    private static (string Bucket, string Key) ParseSourceUri(string sourceUri)
    {
        if (!sourceUri.StartsWith(Scheme, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{sourceUri} is not an s3:// address.", nameof(sourceUri));
        }

        var rest = sourceUri[Scheme.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1)
        {
            throw new ArgumentException($"{sourceUri} has no bucket or no key.", nameof(sourceUri));
        }

        return (rest[..slash], rest[(slash + 1)..]);
    }

    /// <summary>An empty prefix stays empty (the bucket root); any other prefix ends with a slash, so keys are listed under it.</summary>
    private static string NormalizePrefix(string prefix) =>
        prefix.Length == 0 || prefix.EndsWith('/') ? prefix : prefix + "/";
}
