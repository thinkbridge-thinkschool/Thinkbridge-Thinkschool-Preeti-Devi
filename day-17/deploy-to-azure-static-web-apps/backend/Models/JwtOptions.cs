namespace QuotesApi.Models;

/// <summary>
/// Typed configuration for JWT settings.
/// Bound from the "Jwt" section in appsettings.json.
/// Secrets (Key) come from dotnet user-secrets in dev, Key Vault in prod.
/// </summary>
public record JwtOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(5);
}
