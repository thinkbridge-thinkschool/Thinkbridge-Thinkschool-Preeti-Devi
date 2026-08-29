using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Auth;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Quotes")
            ?? "Data Source=/tmp/quotes.db";

        services.AddDbContext<QuoteDbContext>(options =>
            options.UseSqlite(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        services.AddSingleton<IClock, SystemClock>();
        
        services.AddSingleton<IAuthorizationHandler, QuoteOwnerHandler>();

        // ── IOptions<JwtOptions> — typed config from "Jwt" section ──
        // Secrets never go in appsettings.json.
        // Local dev:  dotnet user-secrets set Jwt:Key "..."
        // Prod:       Key Vault references in env vars.
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // Read the Jwt section for auth setup
        var jwtSection = configuration.GetSection("Jwt");
        var jwtKey = jwtSection["Key"]!;
        var jwtIssuer = jwtSection["Issuer"]!;
        var jwtAudience = jwtSection["Audience"]!;

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Smart";
            options.DefaultChallengeScheme = "Smart";
        })
        .AddPolicyScheme("Smart", "Entra ID or Internal JWT", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    var jwtHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    if (jwtHandler.CanReadToken(token))
                    {
                        var jwtToken = jwtHandler.ReadJwtToken(token);
                        if (jwtToken.Issuer.Contains("login.microsoftonline.com") || 
                            jwtToken.Issuer.Contains("sts.windows.net"))
                        {
                            return "Entra";
                        }
                    }
                }
                return "Internal";
            };
        })
        .AddJwtBearer("Entra", options =>
        {
            var tenantId = configuration["EntraId:TenantId"];
            options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            options.Audience = configuration["EntraId:ClientId"];
        })
        .AddJwtBearer("Internal", options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
            };
        });

        services.AddAuthorization(options =>
        {
            // Day 17: two callers can legitimately reach this policy now —
            // a signed-in user's self-issued "Internal" JWT (scope=quotes.write),
            // or a Managed-Identity-issued "Entra" token presented by a trusted
            // server-side proxy, which carries an app role instead of a scope
            // claim (app-only tokens have no user to have "signed in", so there
            // is no "scope" claim at all — only "roles").
            // Whether the JWT's "roles" claim shows up as literal claim type
            // "roles" or gets renamed to ClaimTypes.Role depends on which
            // token handler is active (JsonWebTokenHandler, the .NET 8+
            // default, does NOT remap by default; the legacy
            // JwtSecurityTokenHandler does) — checked both ways instead of
            // assuming one, since guessing wrong here silently 403s every
            // Managed Identity caller.
            options.AddPolicy("can-edit-quotes", p => p.RequireAssertion(ctx =>
                ctx.User.HasClaim("scope", "quotes.write") ||
                ctx.User.HasClaim("roles", "Quotes.Api.Access") ||
                ctx.User.IsInRole("Quotes.Api.Access")));
            options.AddPolicy("OwnerOnly", p => p.Requirements.Add(new QuoteOwnerRequirement()));
        });

        return services;
    }
}

