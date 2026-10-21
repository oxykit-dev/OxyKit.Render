using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OxyKit.Render.Core;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class TemplateStoreTests : IDisposable
{
    private readonly string _root;

    public TemplateStoreTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "phaseflow-templates-" + Guid.NewGuid().ToString("N")
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
    public void Load_ReadsTemplateAndContract()
    {
        WriteTemplate("invoice", "<p>{{ name }}</p>", """{"type":"object"}""");
        var store = CreateStore();

        var def = store.Load("invoice");

        Assert.Equal("invoice", def.Id);
        Assert.Contains("{{ name }}", def.Html);
        Assert.Contains("\"type\"", def.ContractJson);
    }

    [Fact]
    public void Load_RejectsMissingTemplate()
    {
        var store = CreateStore();
        var ex = Assert.Throws<ArgumentException>(() => store.Load("missing"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsPathTraversal()
    {
        var store = CreateStore();
        var ex = Assert.Throws<ArgumentException>(() => store.Load("../etc"));
        Assert.Contains("templateId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RequiresContract()
    {
        var dir = Path.Combine(_root, "noconstract");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "template.html"), "<p>x</p>");

        var store = CreateStore();
        var ex = Assert.Throws<ArgumentException>(() => store.Load("noconstract"));
        Assert.Contains("contract.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void List_ReturnsTemplatesWithContractFlag()
    {
        WriteTemplate("invoice", "<p>a</p>", "{}");
        var incomplete = Path.Combine(_root, "draft");
        Directory.CreateDirectory(incomplete);
        File.WriteAllText(Path.Combine(incomplete, "template.html"), "<p>b</p>");

        var list = CreateStore().List();

        Assert.Contains(list, t => t.Id == "invoice" && t.HasContract);
        Assert.Contains(list, t => t.Id == "draft" && !t.HasContract);
    }

    private void WriteTemplate(string id, string html, string contract)
    {
        var dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "template.html"), html);
        File.WriteAllText(Path.Combine(dir, "contract.json"), contract);
    }

    private TemplateStore CreateStore()
    {
        var options = Options.Create(
            new RenderingOptions
            {
                TemplatesRoot = _root,
                MaxTemplateChars = 10_000,
                MaxContractChars = 10_000,
            }
        );
        return new TemplateStore(options, new TestHostEnvironment());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OxyKit.Render.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
