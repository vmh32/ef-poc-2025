using EdiConsole.Mapping;
using EdiConsole.Parsing;
using EdiConsole.Persistence;
using EdiConsole.Sourcing;
using EdiConsole.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EdiConsole.Ingestion;

public static class IngestionServiceExtensions
{
    /// <summary>
    /// Registers the ingestion workflow: the facade and its options, plus everything the facade runs
    /// (the file source, the parser, the validator, the mapper and the database).
    /// </summary>
    public static IServiceCollection AddIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IngestionOptions>()
            .BindConfiguration(IngestionOptions.SectionName)
            .Validate(
                options => options.SnipLevel is >= 1 and <= 4,
                "Ingestion:SnipLevel must be 1–4 (EdiFabric's SNIP levels).");

        // Stateless: one instance for the whole run.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEdiParser, EdiFabricParser>();
        services.AddSingleton<ITransactionValidator, EdiFabricValidator>();
        services.AddSingleton<IClaimMapper, ClaimMapper>();

        services.AddFileSource();
        services.AddEdiPersistence(configuration);

        // Scoped: one per file (see the loop in FolderIngestion). The facade's DbContext tracks what it saves,
        // so it must not outlive a file.
        services.AddScoped<IIngestionService, IngestionService>();

        // Stateless: a run keeps its state in locals, so one instance is enough.
        services.AddSingleton<FolderIngestion>();

        return services;
    }
}
