using EdiFabric;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EdiConsole.Configuration;

public static class EdiFabricServiceExtensions
{
    /// <summary>Binds the "EdiFabric" section and validates it at startup.</summary>
    public static IServiceCollection AddEdiFabric(this IServiceCollection services)
    {
        services.AddOptions<EdiFabricOptions>()
            .BindConfiguration(EdiFabricOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SerialKey),
                "EdiFabric:SerialKey is missing. For local development run: dotnet user-secrets set \"EdiFabric:SerialKey\" \"<your key>\"");

        return services;
    }

    /// <summary>
    /// Sets EdiFabric's license. The license is process-wide (static), so this is called once, after the host is built
    /// and before any EDI is read or written.
    /// </summary>
    public static void ApplyEdiFabricLicense(this IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<EdiFabricOptions>>().Value;
        License.SetSerial(options.SerialKey);
    }
}
