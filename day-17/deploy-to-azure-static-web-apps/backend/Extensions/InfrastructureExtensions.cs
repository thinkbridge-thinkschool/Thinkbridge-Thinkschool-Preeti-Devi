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

        // ── CORS (Day 17) ──
        // The Angular bundle is served from a different origin (the Static Web App)
        // than this API, so every browser call to /api/auth/* is cross-origin and is
        // preflighted. Origins come from configuration ("Cors:AllowedOrigins") rather
        // than a literal, because the SWA hostname is assigned at deploy time — see
        // day-17/.../scripts/deploy.sh, which sets Cors__AllowedOrigins__0.
        // No AllowCredentials: the session travels in an Authorization header, not a
        // cookie, so credentialed CORS is not needed and "*"-style wildcarding stays
        // off the table either way.
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

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
            // Two different callers legitimately reach this policy and they do not
            // carry the same claim:
            //   • a human, signed in through /api/auth/login  → self-issued JWT with
            //     a "scope" claim of "quotes.write" (the "Internal" scheme);
            //   • the Day-17 Managed Identity proxy            → Entra-issued token with
            //     an app role of "Quotes.Api.Access" (the "Entra" scheme). An app-only
            //     token has no user and therefore no scope claim at all, so requiring
            //     "scope" alone rejected it with a 403 — that was the Day-17 bug.
            // ASP.NET maps the "roles" claim onto ClaimTypes.Role, so both spellings
            // are checked here rather than relying on that mapping being on.
            options.AddPolicy("can-edit-quotes", p => p.RequireAssertion(context =>
                context.User.HasClaim("scope", "quotes.write")
                || context.User.HasClaim(System.Security.Claims.ClaimTypes.Role, "Quotes.Api.Access")
                || context.User.HasClaim("roles", "Quotes.Api.Access")));
            options.AddPolicy("OwnerOnly", p => p.Requirements.Add(new QuoteOwnerRequirement()));
        });

        return services;
    }
}

