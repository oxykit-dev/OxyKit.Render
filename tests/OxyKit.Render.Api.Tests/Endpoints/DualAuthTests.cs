using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace OxyKit.Render.Api.Tests.Endpoints;

public sealed class DualAuthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "test-jwt-signing-key-at-least-32-chars!!";
    private const string JwtIssuer = "OxyKit.Mail";
    private const string JwtAudience = "OxyKit.Mail";

    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        builder.UseSetting("Security:ApiKey", "test-key");
        builder.UseSetting("Security:RequireLocalRequests", "false");
        builder.UseSetting("Security:Jwt:Key", JwtKey);
        builder.UseSetting("Security:Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Security:Jwt:Audience", JwtAudience);
    });

    [Fact]
    public async Task Formats_SucceedsWithBearerAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Admin")
        );

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Formats_SucceedsWithApiKey_WhenJwtAlsoConfigured()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Formats_WrongApiKey_DoesNotFallThroughToBearer()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "not-the-key");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Admin")
        );

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Formats_RejectsNeither()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("API key", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Formats_RejectsInvalidBearer()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "not-a-jwt"
        );

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TemplatesList_AllowsUser_ForbidsSender()
    {
        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("User")
        );
        var userResponse = await userClient.GetAsync(
            "/templates",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);

        var senderClient = _factory.CreateClient();
        senderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Sender")
        );
        var senderResponse = await senderClient.GetAsync(
            "/templates",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Forbidden, senderResponse.StatusCode);
    }

    [Fact]
    public async Task TemplateWrite_RequiresAdmin_AllowsApiKey()
    {
        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("User")
        );
        using var userContent = new ByteArrayContent([0x50, 0x4b]);
        userContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        var userResponse = await userClient.PutAsync(
            "/templates/dual-auth-pack",
            userContent,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Admin")
        );
        // Bad zip → 400 after auth (proves Admin cleared the role gate)
        using var adminContent = new ByteArrayContent([0x50, 0x4b]);
        adminContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        var adminResponse = await adminClient.PutAsync(
            "/templates/dual-auth-pack",
            adminContent,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.BadRequest, adminResponse.StatusCode);

        var keyClient = _factory.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");
        using var keyContent = new ByteArrayContent([0x50, 0x4b]);
        keyContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        var keyResponse = await keyClient.PutAsync(
            "/templates/dual-auth-pack",
            keyContent,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.BadRequest, keyResponse.StatusCode);
    }

    [Fact]
    public async Task Render_AllowsSenderJwt()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Sender")
        );

        var response = await client.PostAsJsonAsync(
            "/render/table/html",
            new
            {
                sheets = new[]
                {
                    new
                    {
                        name = "Orders",
                        headers = new[] { "Id" },
                        rows = new object[][] { [1] },
                    },
                },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_DocumentsBearerAndApiKey()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        var schemes = doc.RootElement.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(schemes.TryGetProperty("ApiKey", out _));
        Assert.True(schemes.TryGetProperty("Bearer", out _));
    }

    private static string CreateToken(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "test-user"),
            new(ClaimTypes.Name, "tester"),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ErrorPayload(string Error);
}
