using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OxyKit.Render.Core;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class TemplatePackTests : IDisposable
{
    private readonly string _root;

    public TemplatePackTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "oxykit-template-pack-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveFromZip_WritesPackAndLoadSucceeds()
    {
        var store = CreateStore();
        await using var zip = CreateZip(
            ("template.html", "<p>{{ name }}</p>"),
            ("contract.json", """{"type":"object","properties":{"name":{"type":"string"}}}""")
        );

        var info = await store.SaveFromZipAsync("demo", zip, TestContext.Current.CancellationToken);

        Assert.Equal("demo", info.Id);
        Assert.True(info.HasContract);
        var def = store.Load("demo");
        Assert.Contains("{{ name }}", def.Html);
    }

    [Fact]
    public async Task SaveFromZip_StripsSingleRootFolder()
    {
        var store = CreateStore();
        await using var zip = CreateZip(
            ("invoice/template.html", "<p>x</p>"),
            ("invoice/contract.json", "{}")
        );

        await store.SaveFromZipAsync("invoice", zip, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(Path.Combine(_root, "invoice", "template.html")));
    }

    [Fact]
    public async Task SaveFromZip_RejectsMissingContract()
    {
        var store = CreateStore();
        await using var zip = CreateZip(("template.html", "<p>x</p>"));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveFromZipAsync("bad", zip, TestContext.Current.CancellationToken)
        );
        Assert.Contains("contract.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveFromZip_RejectsPathTraversalEntry()
    {
        var store = CreateStore();
        await using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../evil.html");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("<p>no</p>");
        }

        ms.Position = 0;
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveFromZipAsync("evil", ms, TestContext.Current.CancellationToken)
        );
        Assert.Contains("unsafe", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RemovesTemplate()
    {
        var store = CreateStore();
        await using var zip = CreateZip(
            ("template.html", "<p>x</p>"),
            ("contract.json", "{}")
        );
        await store.SaveFromZipAsync("gone", zip, TestContext.Current.CancellationToken);

        await store.DeleteAsync("gone", TestContext.Current.CancellationToken);

        var ex = Assert.Throws<ArgumentException>(() => store.Load("gone"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteZip_RoundTripsFiles()
    {
        var store = CreateStore();
        await using var zip = CreateZip(
            ("template.html", "<p>x</p>"),
            ("contract.json", "{}"),
            ("partials/foot.html", "<footer/>"),
            ("assets/logo.svg", "<svg/>")
        );
        await store.SaveFromZipAsync("round", zip, TestContext.Current.CancellationToken);

        await using var outZip = new MemoryStream();
        await store.WriteZipAsync("round", outZip, TestContext.Current.CancellationToken);
        outZip.Position = 0;
        using var archive = new ZipArchive(outZip, ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet();
        Assert.Contains("template.html", names);
        Assert.Contains("contract.json", names);
        Assert.Contains("partials/foot.html", names);
        Assert.Contains("assets/logo.svg", names);
    }

    [Fact]
    public async Task SaveAsset_NormalizesLabelAndDeleteRemoves()
    {
        var store = CreateStore();
        await using var zip = CreateZip(
            ("template.html", "<p>x</p>"),
            ("contract.json", "{}")
        );
        await store.SaveFromZipAsync("branded", zip, TestContext.Current.CancellationToken);

        await using var svg = new MemoryStream(Encoding.UTF8.GetBytes("<svg/>"));
        var path = await store.SaveAssetAsync(
            "branded",
            "logo.svg",
            svg,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("assets/logo.svg", path);
        Assert.True(File.Exists(Path.Combine(_root, "branded", "assets", "logo.svg")));

        await using var svg2 = new MemoryStream(Encoding.UTF8.GetBytes("<svg id='2'/>"));
        var path2 = await store.SaveAssetAsync(
            "branded",
            "assets/logo.svg",
            svg2,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("assets/logo.svg", path2);

        await store.DeleteAssetAsync("branded", "logo.svg", TestContext.Current.CancellationToken);
        Assert.False(File.Exists(Path.Combine(_root, "branded", "assets", "logo.svg")));
    }

    [Fact]
    public async Task SaveAsset_RejectsDisallowedExtension()
    {
        var store = CreateStore();
        await using var zip = CreateZip(
            ("template.html", "<p>x</p>"),
            ("contract.json", "{}")
        );
        await store.SaveFromZipAsync("x", zip, TestContext.Current.CancellationToken);

        await using var body = new MemoryStream(Encoding.UTF8.GetBytes("not-an-image"));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAssetAsync("x", "note.txt", body, TestContext.Current.CancellationToken)
        );
        Assert.Contains("allowlisted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private TemplateStore CreateStore()
    {
        var options = Options.Create(
            new RenderingOptions
            {
                TemplatesRoot = _root,
                MaxTemplateChars = 10_000,
                MaxContractChars = 10_000,
                MaxTemplatePartialChars = 10_000,
                MaxTemplateAssetBytes = 50_000,
                MaxTemplatePackBytes = 1_000_000,
            }
        );
        return new TemplateStore(options, new TestHostEnvironment());
    }

    private static MemoryStream CreateZip(params (string Path, string Content)[] files)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        ms.Position = 0;
        return ms;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OxyKit.Render.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
