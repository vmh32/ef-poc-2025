using EdiConsole.Sourcing;
using EdiConsole.Tests.Support;
using Microsoft.Extensions.Options;

namespace EdiConsole.Tests.Sourcing;

/// <summary><see cref="LocalFolderCheck"/>: it names the folder, which must exist; a missing one is logged with its full path.</summary>
public sealed class LocalFolderCheckTests
{
    [Fact]
    public async Task An_existing_folder_passes_and_the_check_names_it()
    {
        var folder = Directory.CreateTempSubdirectory("edi-check-");
        try
        {
            var logger = new RecordingLogger<LocalFolderCheck>();
            var output = new StringWriter();

            var passed = await CreateCheck(folder.FullName, logger).PassesAsync(output);

            Assert.True(passed);
            Assert.Equal($"Using folder {folder.FullName}", output.ToString().TrimEnd());
            Assert.Empty(logger.Entries);
        }
        finally
        {
            folder.Delete();
        }
    }

    [Fact]
    public async Task A_missing_folder_fails_and_both_the_report_and_the_event_name_its_full_path()
    {
        var setting = "no-such-folder-" + Guid.NewGuid().ToString("N")[..8];    // relative, like a typical setting
        var logger = new RecordingLogger<LocalFolderCheck>();
        var output = new StringWriter();

        var passed = await CreateCheck(setting, logger).PassesAsync(output);

        Assert.False(passed);
        Assert.Equal($"Using folder {Path.GetFullPath(setting)}", output.ToString().TrimEnd());
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("FolderNotFound", entry.EventId.Name);
        Assert.Contains(Path.GetFullPath(setting), entry.Message);    // resolved against the current directory
        Assert.Contains($"is {setting},", entry.Message);
    }

    private static LocalFolderCheck CreateCheck(string folder, RecordingLogger<LocalFolderCheck> logger) =>
        new(Options.Create(new FileSourceOptions { Folder = folder }), logger);
}
