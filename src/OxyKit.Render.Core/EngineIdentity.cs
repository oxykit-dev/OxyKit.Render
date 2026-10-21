using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using OxyKit.Render.Core.Outputs;
using PeachPDF;
using Scriban;

namespace OxyKit.Render.Core;

/// <summary>
/// Product version plus a hash of output-affecting dependency pins (and print baseline).
/// Same <see cref="Version"/> implies the same pin set when release discipline is followed;
/// <see cref="Fingerprint"/> still records the concrete set for mismatch diagnosis.
/// </summary>
public sealed class EngineIdentity
{
    public string Version { get; }
    public string Fingerprint { get; }

    /// <summary>Canonical pin lines used to derive <see cref="Fingerprint"/> (stable order).</summary>
    public IReadOnlyList<string> Components { get; }

    private EngineIdentity(string version, string fingerprint, IReadOnlyList<string> components)
    {
        Version = version;
        Fingerprint = fingerprint;
        Components = components;
    }

    public static EngineIdentity Create()
    {
        var coreAssembly = typeof(EngineIdentity).Assembly;
        var version = GetInformationalVersion(coreAssembly);

        var components = new[]
        {
            $"oxykit.render.core@{version}",
            $"closedxml@{GetInformationalVersion(typeof(XLWorkbook).Assembly)}",
            $"peachpdf@{GetInformationalVersion(typeof(PdfGenerator).Assembly)}",
            $"scriban@{GetInformationalVersion(typeof(Template).Assembly)}",
            $"print-baseline@{Sha256Hex(Encoding.UTF8.GetBytes(PrintBaselineCss.Content))}",
        };

        var material = string.Join('\n', components);
        var fingerprint = Sha256Hex(Encoding.UTF8.GetBytes(material));
        return new EngineIdentity(version, fingerprint, components);
    }

    private static string GetInformationalVersion(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip SourceLink / commit suffix so headers stay stable across local builds of the same package.
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
