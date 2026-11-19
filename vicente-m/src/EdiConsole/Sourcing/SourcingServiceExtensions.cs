using Microsoft.Extensions.DependencyInjection;

namespace EdiConsole.Sourcing;

public static class SourcingServiceExtensions
{
    /// <summary>Binds the "FileSource" section, validates it, and registers the local folder as the file source.</summary>
    public static IServiceCollection AddLocalFolderSource(this IServiceCollection services)
    {
        services.AddOptions<FileSourceOptions>()
            .BindConfiguration(FileSourceOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Folder),
                "FileSource:Folder is missing. Set it in appsettings.<Environment>.json or with the FileSource__Folder environment variable.");

        services.AddSingleton<IEdiFileSource, LocalFolderFileSource>();   // stateless: one instance for the whole run

        return services;
    }
}
