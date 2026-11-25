using EdiConsole.Sourcing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EdiConsole.Tests.Sourcing;

/// <summary>Which source FileSource:Kind selects, and the settings it checks when the source is first needed.</summary>
public sealed class SourcingServiceExtensionsTests
{
    [Fact]
    public void The_Local_kind_resolves_the_local_folder_source() 
    {
        using var provider = Build(new() { ["FileSource:Folder"] = "samples" }); //the default kind is Local, so no need to set it

        Assert.IsType<LocalFolderFileSource>(provider.GetRequiredService<IEdiFileSource>());
        Assert.IsType<LocalFolderCheck>(provider.GetRequiredService<IFileSourceCheck>());
    }

    [Fact]
    public void The_S3_kind_resolves_the_S3_source_and_needs_no_Folder()
    {
        using var provider = Build(new()
        {
            ["FileSource:Kind"] = "S3",
            ["S3Source:BucketName"] = "edi-bucket",
            ["S3Source:Prefix"] = "incoming",
            ["S3Source:Region"] = "us-east-1",
        });

        var source = provider.GetRequiredService<IEdiFileSource>();

        Assert.IsType<S3FileSource>(source);
        Assert.Equal("s3://edi-bucket/incoming/", source.Description);
        Assert.IsType<S3SourceCheck>(provider.GetRequiredService<IFileSourceCheck>());
    }

    [Fact]
    public void A_Local_run_without_a_Folder_names_the_setting()
    {
        using var provider = Build(new() { ["FileSource:Kind"] = "Local" });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IEdiFileSource>());
        Assert.Contains("FileSource:Folder", ex.Message);
    }

    [Fact]
    public void An_S3_run_without_a_BucketName_names_the_setting()
    {
        using var provider = Build(new() { ["FileSource:Kind"] = "S3", ["S3Source:Region"] = "us-east-1" });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IEdiFileSource>());
        Assert.Contains("S3Source:BucketName", ex.Message);
    }

    [Fact]
    public void An_S3_ServiceUrl_without_a_scheme_is_rejected()
    {
        using var provider = Build(new()
        {
            ["FileSource:Kind"] = "S3",
            ["S3Source:BucketName"] = "edi-bucket",
            ["S3Source:Region"] = "us-east-1",
            ["S3Source:ServiceUrl"] = "127.0.0.1:5001",
        });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IEdiFileSource>());
        Assert.Contains("S3Source:ServiceUrl", ex.Message);
    }

    [Fact]
    public void An_S3_ServiceUrl_with_a_scheme_is_accepted()
    {
        using var provider = Build(new()
        {
            ["FileSource:Kind"] = "S3",
            ["S3Source:BucketName"] = "edi-bucket",
            ["S3Source:Region"] = "us-east-1",
            ["S3Source:ServiceUrl"] = "http://127.0.0.1:5001",
        });

        Assert.IsType<S3FileSource>(provider.GetRequiredService<IEdiFileSource>());
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddLogging();                          // the checks log; the host adds this in the app
        services.AddFileSource();
        return services.BuildServiceProvider();
    }
}
