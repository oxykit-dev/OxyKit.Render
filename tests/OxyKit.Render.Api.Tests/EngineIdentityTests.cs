using OxyKit.Render.Core;

namespace OxyKit.Render.Api.Tests;

public sealed class EngineIdentityTests
{
    [Fact]
    public void Create_IncludesQrcoderAndBarcoderPins()
    {
        var identity = EngineIdentity.Create();
        Assert.Contains(identity.Components, c => c.StartsWith("qrcoder@", StringComparison.Ordinal));
        Assert.Contains(identity.Components, c => c.StartsWith("barcoder@", StringComparison.Ordinal));
        Assert.Equal(64, identity.Fingerprint.Length);
    }
}
