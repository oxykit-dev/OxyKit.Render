namespace OxyKit.Render.Core.Outputs;

/// <summary>
/// Loads the shared print baseline stylesheet embedded from <c>assets/print-baseline.css</c>.
/// </summary>
internal static class PrintBaselineCss
{
    private const string ResourceName = "OxyKit.Render.Core.assets.print-baseline.css";

    public static string Content { get; } = Load();

    private static string Load()
    {
        var assembly = typeof(PrintBaselineCss).Assembly;
        using var stream =
            assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found. "
                    + "Ensure assets/print-baseline.css is marked as EmbeddedResource."
            );

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
