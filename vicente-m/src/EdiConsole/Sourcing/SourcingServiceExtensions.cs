using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EdiConsole.Sourcing;

public static class SourcingServiceExtensions
{
    /// <summary>
    /// Binds and validates the "FileSource" and "S3Source" sections, and registers the source that FileSource:Kind selects.
    /// The S3 settings and client are read only when the S3 source is used, so a local run needs no S3Source section.
    /// </summary>
    public static IServiceCollection AddFileSource(this IServiceCollection services)
    {
        services.AddOptions<FileSourceOptions>()
            .BindConfiguration(FileSourceOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SearchPattern),
                "FileSource:SearchPattern is empty. Use a pattern such as *.edi.")
            .Validate(
                options => options.Kind != FileSourceKind.Local || !string.IsNullOrWhiteSpace(options.Folder),
                "FileSource:Folder is missing. Set it in appsettings.<Environment>.json or with the FileSource__Folder environment variable.");

        services.AddOptions<S3SourceOptions>()
            .BindConfiguration(S3SourceOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.BucketName),
                "S3Source:BucketName is missing. Set it in appsettings.<Environment>.json or with the S3Source__BucketName environment variable.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Region),
                "S3Source:Region is missing, e.g. us-east-1.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ServiceUrl) || IsHttpUrl(options.ServiceUrl),
                "S3Source:ServiceUrl must be an http or https address, e.g. http://127.0.0.1:5001 for moto.");

        services.AddSingleton<LocalFolderFileSource>();
        services.AddSingleton<S3FileSource>();
        services.AddSingleton<IAmazonS3>(provider =>
            S3ClientFactory.Create(provider.GetRequiredService<IOptions<S3SourceOptions>>().Value));

        // Chosen from the options the first time the file source is needed. Both sources are stateless: one of each is enough.
        services.AddSingleton<IEdiFileSource>(provider =>
        {
            var kind = provider.GetRequiredService<IOptions<FileSourceOptions>>().Value.Kind;
            return kind switch
            {
                FileSourceKind.Local => provider.GetRequiredService<LocalFolderFileSource>(),
                FileSourceKind.S3 => provider.GetRequiredService<S3FileSource>(),
                _ => throw new InvalidOperationException($"FileSource:Kind {kind} is not supported."),
            };
        });

        return services;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
