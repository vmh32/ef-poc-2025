using EdiConsole.Sourcing;
using EdiConsole.Tests.Support;

namespace EdiConsole.Tests.Sourcing;

/// <summary>
/// <see cref="S3FileSource"/> against a real S3 API: moto on <see cref="MotoBucket.Endpoint"/>.
/// Run only these with: dotnet test --filter "Category=Moto". Each test makes its own bucket and deletes it afterwards.
/// </summary>
[Trait("Category", "Moto")]
public sealed class S3FileSourceMotoTests
{
    private static readonly byte[] NotEdi = "not an EDI file"u8.ToArray();

    [Fact]
    public async Task Lists_only_the_matching_files_directly_under_the_prefix_and_reads_them_exactly()
    {
        await using var bucket = await MotoBucket.CreateAsync();
        var sample = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "samples", "837p-sample.edi"));
        await bucket.PutAsync("incoming/837p-sample.edi", sample);
        await bucket.PutAsync("incoming/x.edix", NotEdi);           // wrong extension
        await bucket.PutAsync("incoming/y.edia", NotEdi);           // wrong extension
        await bucket.PutAsync("incoming/sub/nested.edi", NotEdi);   // in a subfolder
        await bucket.PutAsync("outside/other.edi", NotEdi);         // outside the prefix
        var source = bucket.CreateSource("incoming/", "*.edi");   // SearchPattern: only *.edi names are listed

        var file = Assert.Single(await source.ListFilesAsync());

        Assert.Equal("837p-sample.edi", file.Name);
        Assert.Equal($"s3://{bucket.Name}/incoming/837p-sample.edi", file.SourceUri);
        Assert.Equal($"s3://{bucket.Name}/incoming/", source.Description);
        await using var stream = await source.OpenReadAsync(file);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        Assert.Equal(sample, copy.ToArray());
    }

    [Fact]
    public async Task A_prefix_without_a_trailing_slash_finds_the_same_files()
    {
        await using var bucket = await MotoBucket.CreateAsync();
        await bucket.PutAsync("incoming/b.edi", NotEdi);
        await bucket.PutAsync("incoming/a.edi", NotEdi);

        var names = (await bucket.CreateSource("incoming", "*.edi").ListFilesAsync()).Select(file => file.Name);

        Assert.Equal(["a.edi", "b.edi"], names);
    }
}
